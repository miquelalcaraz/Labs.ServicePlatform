using Microsoft.Extensions.Logging;

using QltS.Shared.EventBus.Abstractions;
using QltS.Shared.Messaging.IntegrationEvents;
using QltS.Shared.Messaging.Outbox;
using QltS.Shared.Telemetry.Meters;

using System.Diagnostics;

namespace QltS.Shared.Persistence.EF.Outbox;

/// <summary>
/// Service that combines the outbox pattern with event publishing.
/// Coordinates saving events to the outbox and publishing them through the event bus.
/// </summary>
public class EventBusOutboxService<TContext> : IEventBusOutboxService
    where TContext : QltDBContextBase
{
    private readonly IEventBus _eventBus;
    private readonly TContext _dbContext;
    private readonly IOutboxMessageService _eventLogService;
    private readonly ILogger<IOutboxMessageService> _logger;

    public EventBusOutboxService(
        IEventBus eventBus,
        TContext dbContext,
        IOutboxMessageService integrationEventLogService,
        ILogger<IOutboxMessageService> logger)
    {
        _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _eventLogService = integrationEventLogService ?? throw new ArgumentNullException(nameof(integrationEventLogService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task PublishEventsThroughEventBusAsync(Guid transactionId)
    {
        var pendingLogEvents = await _eventLogService.RetrieveEventLogsPendingToPublishAsync(transactionId);
        var eventsList = pendingLogEvents.ToList();

        if (eventsList.Count == 0)
        {
            return;
        }

        // Record batch size metric
        ApplicationDiagnostics.OutboxBatchSize.Record(eventsList.Count);

        // Start activity for outbox publishing
        using var activity = ApplicationDiagnostics.ActivitySource.StartActivity(
            name: "Publish Outbox Events",
            kind: ActivityKind.Internal);

        activity?.SetTag("transaction.id", transactionId);
        activity?.SetTag("outbox.event_count", eventsList.Count);

        var successCount = 0;
        var failureCount = 0;

        foreach (var logEvt in eventsList)
        {
            _logger.LogInformation("Publishing integration event: {IntegrationEventId} - ({@IntegrationEvent})", logEvt.EventId, logEvt.IntegrationEvent);

            try
            {
                await _eventLogService.MarkEventAsInProgressAsync(logEvt.EventId);
                await _eventBus.PublishAsync(logEvt.IntegrationEvent);
                await _eventLogService.MarkEventAsPublishedAsync(logEvt.EventId);
                successCount++;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error publishing integration event: {IntegrationEventId}", logEvt.EventId);

                await _eventLogService.MarkEventAsFailedAsync(logEvt.EventId);
                failureCount++;

                // Record failed event metric
                ApplicationDiagnostics.OutboxEventsFailed.Add(1,
                    new KeyValuePair<string, object?>("event_type", logEvt.IntegrationEvent.GetType().Name));
            }
        }

        activity?.SetTag("outbox.published_count", successCount);
        activity?.SetTag("outbox.failed_count", failureCount);

        if (failureCount > 0)
        {
            activity?.SetStatus(ActivityStatusCode.Error, $"{failureCount} events failed to publish");
        }
    }

    public async Task AddAndSaveEventAsync(IntegrationEvent evt)
    {
        // Start activity for saving event to outbox
        using var activity = ApplicationDiagnostics.ActivitySource.StartActivity(
            name: "Save Event to Outbox",
            kind: ActivityKind.Internal);

        var eventType = evt.GetType().Name;
        activity?.SetTag("event.type", eventType);
        activity?.SetTag("event.id", evt.Id);

        _logger.LogInformation("Enqueuing integration event {IntegrationEventId} to repository ({@IntegrationEvent})", evt.Id, evt);

        var stopwatch = Stopwatch.StartNew();

        try
        {
            await _eventLogService.SaveEventAsync(evt, _dbContext.GetCurrentTransaction());
            stopwatch.Stop();

            var elapsedMs = stopwatch.Elapsed.TotalMilliseconds;

            // Record metrics
            ApplicationDiagnostics.OutboxEventsSaved.Add(1,
                new KeyValuePair<string, object?>("event_type", eventType));

            ApplicationDiagnostics.OutboxOperationDuration.Record(elapsedMs,
                new KeyValuePair<string, object?>("operation", "save"));

            activity?.SetTag("outbox.saved", true);
            activity?.SetTag("outbox.save_duration_ms", elapsedMs);
        }
        catch (Exception ex)
        {
            activity?.SetTag("outbox.saved", false);
            activity?.SetTag("error.type", ex.GetType().FullName);
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            throw;
        }
    }
}
