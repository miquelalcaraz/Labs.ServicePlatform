using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Sirius.Platform.Messaging.IntegrationEvents;
using Sirius.Platform.Messaging.Models;
using Sirius.Platform.Messaging.Options;
using Sirius.Platform.Meters;
using Sirius.Platform.Persistence;
namespace Sirius.Platform.Messaging.Services;

/// <summary>
/// Database-backed implementation of the transactional outbox pattern.
/// Stores integration events within the same transaction as business data,
/// so events are published at least once; consumers deduplicate them through idempotent handlers.
/// </summary>
public class OutboxMessageService<TContext> : IOutboxMessageService
    where TContext : DbContext, ITransactionalUnitOfWork
{
    private static readonly JsonSerializerOptions s_indentedOptions = new() { WriteIndented = true };
    private static readonly JsonSerializerOptions s_caseInsensitiveOptions = new() { PropertyNameCaseInsensitive = true };

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
        using var activity = ApplicationDiagnostics.ActivitySource.StartActivity(
            name: "Retrieve Pending Events from Outbox",
            kind: ActivityKind.Internal);

        activity?.SetTag("outbox.transaction_id", transactionId);

        var stopwatch = Stopwatch.StartNew();

        var result = await _context.Set<OutboxMessage>()
            .Where(e => e.TransactionId == transactionId && e.Status == OutboxMessageStatus.NotPublished)
            .ToListAsync();

        stopwatch.Stop();
        var elapsedMs = stopwatch.Elapsed.TotalMilliseconds;

        activity?.SetTag("outbox.pending_count", result.Count);
        activity?.SetTag("outbox.retrieve_duration_ms", elapsedMs);

        ApplicationDiagnostics.OutboxOperationDuration.Record(elapsedMs,
            new KeyValuePair<string, object?>("operation", "retrieve"));

        if (result.Count != 0)
        {
            return result.OrderBy(o => o.CreatedAt)
                .Select(e => DeserializePayload(e, activity))
                .ToList();
        }

        return [];
    }

    /// <summary>
    /// Saves an integration event to the transactional outbox by adding it to the change tracker.
    /// </summary>
    public Task SaveEventAsync(IntegrationEvent @event, Guid transactionId)
    {
        using var activity = ApplicationDiagnostics.ActivitySource.StartActivity(
            name: "Save Event to Outbox",
            kind: ActivityKind.Internal);

        activity?.SetTag("outbox.event_type", @event.GetType().Name);
        activity?.SetTag("outbox.event_id", @event.Id.ToString());
        activity?.SetTag("outbox.transaction_id", transactionId.ToString());

        var outboxMessage = new OutboxMessage
        {
            Id = @event.Id,
            CreatedAt = @event.CreationDate,
            TypeName = @event.GetType().FullName!,
            Payload = JsonSerializer.Serialize(@event, @event.GetType(), s_indentedOptions),
            Status = OutboxMessageStatus.NotPublished,
            SendCount = 0,
            TransactionId = transactionId
        };

        _context.Set<OutboxMessage>().Add(outboxMessage);

        ApplicationDiagnostics.OutboxEventsSaved.Add(1,
            new KeyValuePair<string, object?>("event_type", @event.GetType().Name));

        return Task.CompletedTask;
    }

    public Task MarkEventAsPublishedAsync(Guid eventId)
    {
        return UpdateEventStatus(eventId, OutboxMessageStatus.Published);
    }

    public Task MarkEventAsInProgressAsync(Guid eventId)
    {
        return UpdateEventStatus(eventId, OutboxMessageStatus.InProgress);
    }

    public Task MarkEventAsFailedAsync(Guid eventId)
    {
        return UpdateEventStatus(eventId, OutboxMessageStatus.FailedToPublish);
    }

    private async Task UpdateEventStatus(Guid eventId, OutboxMessageStatus status)
    {
        using var activity = ApplicationDiagnostics.ActivitySource.StartActivity(
            name: "Update Outbox Event Status",
            kind: ActivityKind.Internal);

        activity?.SetTag("outbox.event_id", eventId);
        activity?.SetTag("outbox.status", status.ToString());

        var stopwatch = Stopwatch.StartNew();

        var outboxMessage = await _context.Set<OutboxMessage>()
            .SingleOrDefaultAsync(ie => ie.Id == eventId);

        if (outboxMessage == null)
        {
            _logger.LogWarning("Event {EventId} not found when updating status to {Status}", eventId, status);
            return;
        }

        outboxMessage.Status = status;

        if (status == OutboxMessageStatus.InProgress)
        {
            outboxMessage.SendCount++;
            activity?.SetTag("outbox.send_count", outboxMessage.SendCount);
        }

        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException ex)
        {
            _logger.LogWarning(ex,
                "Concurrency conflict updating event {EventId} to status {Status}. Event may have been modified by another process.",
                eventId, status);

            activity?.AddEvent(new ActivityEvent("ConcurrencyConflict",
                tags: new ActivityTagsCollection
                {
                    { "event_id", eventId.ToString() },
                    { "target_status", status.ToString() }
                }));

            throw;
        }

        stopwatch.Stop();
        var elapsedMs = stopwatch.Elapsed.TotalMilliseconds;

        ApplicationDiagnostics.OutboxEventStateChanges.Add(1,
            new KeyValuePair<string, object?>("status", status.ToString()));

        ApplicationDiagnostics.OutboxOperationDuration.Record(elapsedMs,
            new KeyValuePair<string, object?>("operation", "update_status"));

        activity?.SetTag("outbox.update_duration_ms", elapsedMs);
    }

    public async Task<IEnumerable<OutboxMessage>> RetrieveUnpublishedEventsAsync(int batchSize, int maxRetryCount, CancellationToken cancellationToken = default)
    {
        using var activity = ApplicationDiagnostics.ActivitySource.StartActivity(
            name: "Retrieve Unpublished Outbox Events",
            kind: ActivityKind.Internal);

        activity?.SetTag("outbox.batch_size", batchSize);
        activity?.SetTag("outbox.max_retry_count", maxRetryCount);

        var stopwatch = Stopwatch.StartNew();

        var candidateEventIds = await _context.Set<OutboxMessage>()
            .Where(e =>
                (e.Status == OutboxMessageStatus.NotPublished || e.Status == OutboxMessageStatus.FailedToPublish)
                && e.SendCount < maxRetryCount)
            .OrderBy(e => e.CreatedAt)
            .Take(batchSize)
            .Select(e => e.Id)
            .ToListAsync(cancellationToken);

        if (candidateEventIds.Count == 0)
        {
            stopwatch.Stop();
            activity?.SetTag("outbox.unpublished_count", 0);
            activity?.SetTag("outbox.retrieve_duration_ms", stopwatch.Elapsed.TotalMilliseconds);
            return [];
        }

        var claimedCount = await _context.Set<OutboxMessage>()
            .Where(e => candidateEventIds.Contains(e.Id)
                && (e.Status == OutboxMessageStatus.NotPublished || e.Status == OutboxMessageStatus.FailedToPublish)
                && e.SendCount < maxRetryCount)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(e => e.Status, OutboxMessageStatus.InProgress)
                .SetProperty(e => e.SendCount, e => e.SendCount + 1),
                cancellationToken);

        activity?.SetTag("outbox.claimed_count", claimedCount);

        if (claimedCount == 0)
        {
            stopwatch.Stop();
            activity?.SetTag("outbox.unpublished_count", 0);
            activity?.SetTag("outbox.retrieve_duration_ms", stopwatch.Elapsed.TotalMilliseconds);

            _logger.LogDebug("No events were claimed (likely claimed by another process)");
            return [];
        }

        var result = await _context.Set<OutboxMessage>()
            .Where(e => candidateEventIds.Contains(e.Id) && e.Status == OutboxMessageStatus.InProgress)
            .OrderBy(e => e.CreatedAt)
            .ToListAsync(cancellationToken);

        stopwatch.Stop();
        var elapsedMs = stopwatch.Elapsed.TotalMilliseconds;

        activity?.SetTag("outbox.unpublished_count", result.Count);
        activity?.SetTag("outbox.retrieve_duration_ms", elapsedMs);

        ApplicationDiagnostics.OutboxOperationDuration.Record(elapsedMs,
            new KeyValuePair<string, object?>("operation", "retrieve_unpublished"));

        if (result.Count != 0)
        {
            return result.Select(e => DeserializePayload(e, activity))
                .ToList();
        }

        return [];
    }

    private OutboxMessage DeserializePayload(OutboxMessage message, Activity? activity)
    {
        var eventType = _eventTypes.FirstOrDefault(t => t.FullName == message.TypeName);

        if (eventType == null)
        {
            _logger.LogError(
                "Event type '{TypeName}' not registered. Event {EventId} cannot be published.",
                message.TypeName,
                message.Id);

            activity?.AddEvent(new ActivityEvent("EventTypeNotRegistered",
                tags: new ActivityTagsCollection
                {
                    { "event_type_name", message.TypeName },
                    { "event_id", message.Id.ToString() }
                }));

            throw new InvalidOperationException(
                $"Event type '{message.TypeName}' is not registered. " +
                $"Add it to OutboxMessageOptions.RegisterEventTypes()");
        }

        message.DeserializedEvent = JsonSerializer.Deserialize(message.Payload, eventType, s_caseInsensitiveOptions) as IntegrationEvent;
        return message;
    }
}
