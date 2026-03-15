using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using QltSystem.Core.EventBus.Contracts.Events;
using QltSystem.Core.EventBus.TransactionalOutbox.Configuration;
using QltSystem.Core.EventBus.TransactionalOutbox.Models;

namespace QltSystem.Core.EventBus.TransactionalOutbox.Services;

/// <summary>
/// Database-backed implementation of the transactional outbox pattern.
/// Stores integration events within the same transaction as business data,
/// ensuring reliable event publishing with exactly-once delivery semantics.
/// </summary>
public class IntegrationEventLogService<TContext> : IIntegrationEventLogService
    where TContext : DbContext
{
    private readonly TContext _context;
    private readonly IReadOnlyCollection<Type> _eventTypes;
    private readonly ILogger<IIntegrationEventLogService> _logger;

    public IntegrationEventLogService(
        TContext context,
        IOptions<IntegrationEventLogOptions> options,
        ILogger<IIntegrationEventLogService> logger)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _eventTypes = options?.Value?.EventTypes ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<IEnumerable<IntegrationEventLogEntry>> RetrieveEventLogsPendingToPublishAsync(Guid transactionId)
    {
        var result = await _context.Set<IntegrationEventLogEntry>()
            .Where(e => e.TransactionId == transactionId && e.State == EventStateEnum.NotPublished)
            .ToListAsync();

        if (result.Count != 0)
        {
            return result.OrderBy(o => o.CreationTime)
                .Select(e =>
                {
                    var eventType = _eventTypes.FirstOrDefault(t => t.FullName == e.EventTypeName);

                    if (eventType == null)
                    {
                        _logger.LogError(
                            "Event type '{EventTypeName}' not registered. Event {EventId} cannot be published.",
                            e.EventTypeName,
                            e.EventId);

                        throw new InvalidOperationException(
                            $"Event type '{e.EventTypeName}' is not registered. " +
                            $"Add it to IntegrationEventLogOptions.RegisterEventTypes()");
                    }

                    return e.DeserializeJsonContent(eventType);
                })
                .ToList();
        }

        return [];
    }

    public Task SaveEventAsync(IntegrationEvent @event, IDbContextTransaction transaction)
    {
        if (transaction == null) throw new ArgumentNullException(nameof(transaction));

        var eventLogEntry = new IntegrationEventLogEntry(@event, transaction.TransactionId);

        _context.Database.UseTransaction(transaction.GetDbTransaction());
        _context.Set<IntegrationEventLogEntry>().Add(eventLogEntry);

        return _context.SaveChangesAsync();
    }

    public Task MarkEventAsPublishedAsync(Guid eventId)
    {
        return UpdateEventStatus(eventId, EventStateEnum.Published);
    }

    public Task MarkEventAsInProgressAsync(Guid eventId)
    {
        return UpdateEventStatus(eventId, EventStateEnum.InProgress);
    }

    public Task MarkEventAsFailedAsync(Guid eventId)
    {
        return UpdateEventStatus(eventId, EventStateEnum.PublishedFailed);
    }

    private async Task UpdateEventStatus(Guid eventId, EventStateEnum status)
    {
        var eventLogEntry = await _context.Set<IntegrationEventLogEntry>()
            .SingleAsync(ie => ie.EventId == eventId);

        eventLogEntry.State = status;

        if (status == EventStateEnum.InProgress)
            eventLogEntry.TimesSent++;

        await _context.SaveChangesAsync();
    }
}
