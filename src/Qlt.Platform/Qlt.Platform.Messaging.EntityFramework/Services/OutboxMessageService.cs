using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Qlt.Platform.Messaging.Extensions;
using Qlt.Platform.Messaging.IntegrationEvents;
using Qlt.Platform.Messaging.Outbox;
using Qlt.Platform.Meters;
using Qlt.Platform.Persistence;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
namespace Qlt.Platform.Messaging.Services;

/// <summary>
/// Database-backed implementation of the transactional outbox pattern.
/// Stores integration events within the same transaction as business data,
/// ensuring reliable event publishing with exactly-once delivery semantics.
/// </summary>
public class OutboxMessageService<TContext> : IOutboxMessageService
    where TContext : DbContext, ITransactionalUnitOfWork
{
    private readonly TContext _context;
    private readonly IReadOnlyCollection<Type> _eventTypes;
    private readonly ILogger<IOutboxMessageService> _logger;

    public OutboxMessageService(
        TContext context,
        IOptions<OutboxMessageOptions> options,
        ILogger<IOutboxMessageService> logger)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _eventTypes = options?.Value?.EventTypes ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<IEnumerable<OutboxMessage>> RetrieveEventLogsPendingToPublishAsync(Guid transactionId)
    {
        // Start activity for retrieving pending events from outbox
        using var activity = ApplicationDiagnostics.ActivitySource.StartActivity(
            name: "Retrieve Pending Events from Outbox",
            kind: ActivityKind.Internal);

        activity?.SetTag("outbox.transaction_id", transactionId);

        var stopwatch = Stopwatch.StartNew();

        var result = await _context.Set<OutboxMessage>()
            .Where(e => e.TransactionId == transactionId && e.State == EventStateEnum.NotPublished)
            .ToListAsync();

        stopwatch.Stop();
        var elapsedMs = stopwatch.Elapsed.TotalMilliseconds;

        activity?.SetTag("outbox.pending_count", result.Count);
        activity?.SetTag("outbox.retrieve_duration_ms", elapsedMs);

        // Record metric
        ApplicationDiagnostics.OutboxOperationDuration.Record(elapsedMs,
            new KeyValuePair<string, object?>("operation", "retrieve"));

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

                        activity?.AddEvent(new ActivityEvent("EventTypeNotRegistered",
                            tags: new ActivityTagsCollection
                            {
                                { "event_type_name", e.EventTypeName },
                                { "event_id", e.EventId.ToString() }
                            }));

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

    /// <summary>
    /// Saves an integration event to the transactional outbox by adding it to the change tracker.
    /// </summary>
    public Task SaveEventAsync(IntegrationEvent @event, IDbContextTransaction transaction)
    {
        if (transaction == null) throw new ArgumentNullException(nameof(transaction));

        var eventLogEntry = new OutboxMessage(@event, transaction.TransactionId);

        _context.UseTransaction(transaction);
        _context.Set<OutboxMessage>().Add(eventLogEntry);

        return Task.CompletedTask;
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
        // Start activity for updating event status
        using var activity = ApplicationDiagnostics.ActivitySource.StartActivity(
            name: "Update Outbox Event Status",
            kind: ActivityKind.Internal);

        activity?.SetTag("outbox.event_id", eventId);
        activity?.SetTag("outbox.status", status.ToString());

        var stopwatch = Stopwatch.StartNew();

        var eventLogEntry = await _context.Set<OutboxMessage>()
            .SingleAsync(ie => ie.EventId == eventId);

        eventLogEntry.State = status;

        if (status == EventStateEnum.InProgress)
        {
            eventLogEntry.TimesSent++;
            activity?.SetTag("outbox.times_sent", eventLogEntry.TimesSent);
        }

        await _context.SaveChangesAsync();

        stopwatch.Stop();
        var elapsedMs = stopwatch.Elapsed.TotalMilliseconds;

        // Record metrics
        ApplicationDiagnostics.OutboxEventStateChanges.Add(1,
            new KeyValuePair<string, object?>("status", status.ToString()));

        ApplicationDiagnostics.OutboxOperationDuration.Record(elapsedMs,
            new KeyValuePair<string, object?>("operation", "update_status"));

        activity?.SetTag("outbox.update_duration_ms", elapsedMs);
    }
}
