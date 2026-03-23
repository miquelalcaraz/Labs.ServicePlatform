using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using QltS.Shared.Messaging.Inbox;
using QltS.Shared.Telemetry.Activities;

using System.Diagnostics;


namespace QltS.Shared.Persistence.EF.Inbox;

/// <summary>
/// Database-backed implementation of event idempotency service.
/// Uses ProcessedIntegrationEvents table to track which events have been processed by which handlers.
/// </summary>
public class IntegrationEventInboxService<TContext> : IIntegrationEventInbox where TContext : DbContext
{
    private readonly TContext _context;
    private readonly ILogger<IIntegrationEventInbox> _logger;

    public IntegrationEventInboxService(TContext context, ILogger<IIntegrationEventInbox> logger)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<bool> IsProcessedAsync(Guid eventId, string handlerName)
    {
        // Start activity for idempotency check
        using var activity = IdempotencyHelper.StartEventIdempotencyCheck(eventId, handlerName);

        var stopwatch = Stopwatch.StartNew();

        var exists = await _context.Set<IntegrationEventInbox>()
            .AnyAsync(e => e.EventId == eventId && e.HandlerName == handlerName);

        stopwatch.Stop();
        var elapsedMs = stopwatch.Elapsed.TotalMilliseconds;

        // Record idempotency check result
        IdempotencyHelper.RecordCheckResult(activity, exists, elapsedMs);
        IdempotencyHelper.RecordCheckDurationMetric("event", exists, elapsedMs);

        if (exists)
        {
            // Record duplicate detection
            IdempotencyHelper.RecordDuplicateEvent(activity, eventId, handlerName);

            _logger.LogInformation("Event {EventId} already processed by {HandlerName}. Skipping duplicate.", eventId, handlerName);
        }

        return exists;
    }

    public async Task MarkAsProcessedAsync(Guid eventId, string handlerName, string? eventType = null)
    {
        // Start activity for marking event as processed
        using var activity = IdempotencyHelper.StartMarkEventAsProcessed(eventId, handlerName);

        var processedEvent = new IntegrationEventInbox
        {
            EventId = eventId,
            HandlerName = handlerName,
            ProcessedAt = DateTime.UtcNow,
            EventType = eventType
        };

        _context.Set<IntegrationEventInbox>().Add(processedEvent);

        await _context.SaveChangesAsync();

        IdempotencyHelper.RecordEventMarked(activity);

        _logger.LogInformation("Marked event {EventId} as processed by {HandlerName}", eventId, handlerName);
    }
}

