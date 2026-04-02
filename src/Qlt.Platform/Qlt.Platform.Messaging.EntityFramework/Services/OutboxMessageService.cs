using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Qlt.Platform.Messaging.IntegrationEvents;
using Qlt.Platform.Messaging.Models;
using Qlt.Platform.Messaging.Options;
using Qlt.Platform.Meters;
using Qlt.Platform.Persistence;
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
    public Task SaveEventAsync(IntegrationEvent @event, Guid transactionId)
    {
        using var activity = ApplicationDiagnostics.ActivitySource.StartActivity(
            name: "Save Event to Outbox",
            kind: ActivityKind.Internal);

        activity?.SetTag("outbox.event_type", @event.GetType().Name);
        activity?.SetTag("outbox.event_id", @event.Id.ToString());
        activity?.SetTag("outbox.transaction_id", transactionId.ToString());

        var eventLogEntry = new OutboxMessage(@event, transactionId);

        _context.Set<OutboxMessage>().Add(eventLogEntry);

        ApplicationDiagnostics.OutboxEventsSaved.Add(1,
            new KeyValuePair<string, object?>("event_type", @event.GetType().Name));

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
        using var activity = ApplicationDiagnostics.ActivitySource.StartActivity(
            name: "Update Outbox Event Status",
            kind: ActivityKind.Internal);

        activity?.SetTag("outbox.event_id", eventId);
        activity?.SetTag("outbox.status", status.ToString());

        var stopwatch = Stopwatch.StartNew();

        var eventLogEntry = await _context.Set<OutboxMessage>()
            .SingleOrDefaultAsync(ie => ie.EventId == eventId);

        if (eventLogEntry == null)
        {
            _logger.LogWarning("Event {EventId} not found when updating status to {Status}", eventId, status);
            return;
        }

        eventLogEntry.State = status;

        if (status == EventStateEnum.InProgress)
        {
            eventLogEntry.TimesSent++;
            activity?.SetTag("outbox.times_sent", eventLogEntry.TimesSent);
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

        // Step 1: Get candidate event IDs (read-only query)
        var candidateEventIds = await _context.Set<OutboxMessage>()
            .Where(e =>
                (e.State == EventStateEnum.NotPublished || e.State == EventStateEnum.PublishedFailed)
                && e.TimesSent < maxRetryCount)
            .OrderBy(e => e.CreationTime)
            .Take(batchSize)
            .Select(e => e.EventId)
            .ToListAsync(cancellationToken);

        if (candidateEventIds.Count == 0)
        {
            stopwatch.Stop();
            activity?.SetTag("outbox.unpublished_count", 0);
            activity?.SetTag("outbox.retrieve_duration_ms", stopwatch.Elapsed.TotalMilliseconds);
            return [];
        }

        // Step 2: Atomically claim events by transitioning to InProgress
        var claimedCount = await _context.Set<OutboxMessage>()
            .Where(e => candidateEventIds.Contains(e.EventId)
                && (e.State == EventStateEnum.NotPublished || e.State == EventStateEnum.PublishedFailed)
                && e.TimesSent < maxRetryCount)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(e => e.State, EventStateEnum.InProgress)
                .SetProperty(e => e.TimesSent, e => e.TimesSent + 1),
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

        // Step 3: Read the claimed events with their full data
        var result = await _context.Set<OutboxMessage>()
            .Where(e => candidateEventIds.Contains(e.EventId) && e.State == EventStateEnum.InProgress)
            .OrderBy(e => e.CreationTime)
            .ToListAsync(cancellationToken);

        stopwatch.Stop();
        var elapsedMs = stopwatch.Elapsed.TotalMilliseconds;

        activity?.SetTag("outbox.unpublished_count", result.Count);
        activity?.SetTag("outbox.retrieve_duration_ms", elapsedMs);

        ApplicationDiagnostics.OutboxOperationDuration.Record(elapsedMs,
            new KeyValuePair<string, object?>("operation", "retrieve_unpublished"));

        if (result.Count != 0)
        {
            return result.Select(e =>
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
}
