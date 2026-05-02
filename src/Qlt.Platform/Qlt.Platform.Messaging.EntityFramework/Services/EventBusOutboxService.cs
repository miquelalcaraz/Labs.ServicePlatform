using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

using Microsoft.Extensions.Logging;

using Qlt.Platform.EventBus;
using Qlt.Platform.Messaging.IntegrationEvents;
using Qlt.Platform.Meters;
using Qlt.Platform.Persistence;

namespace Qlt.Platform.Messaging.Services;

/// <summary>
/// Service that combines the outbox pattern with event publishing.
/// Coordinates saving events to the outbox and publishing them through the event bus.
/// </summary>
public class EventBusOutboxService<TContext> : IEventBusOutboxService
    where TContext : ITransactionalUnitOfWork
{
    private readonly IEventBus _eventBus;
    private readonly TContext _dbTransaction;
    private readonly IOutboxMessageService _eventLogService;
    private readonly ILogger<IOutboxMessageService> _logger;

    public EventBusOutboxService(
        IEventBus eventBus,
        TContext dbTransaction,
        IOutboxMessageService integrationEventLogService,
        ILogger<IOutboxMessageService> logger)
    {
        _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
        _dbTransaction = dbTransaction ?? throw new ArgumentNullException(nameof(dbTransaction));
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
            _logger.LogInformation("Publishing integration event: {IntegrationEventId} - ({@IntegrationEvent})", logEvt.Id, logEvt.DeserializedEvent);

            try
            {
                await _eventLogService.MarkEventAsInProgressAsync(logEvt.Id);
                await _eventBus.PublishAsync(logEvt.DeserializedEvent);
                await _eventLogService.MarkEventAsPublishedAsync(logEvt.Id);
                successCount++;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error publishing integration event: {IntegrationEventId}", logEvt.Id);

                await _eventLogService.MarkEventAsFailedAsync(logEvt.Id);
                failureCount++;

                ApplicationDiagnostics.OutboxEventsFailed.Add(1,
                    new KeyValuePair<string, object?>("event_type", logEvt.DeserializedEvent.GetType().Name));
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
            var transaction = _dbTransaction.GetCurrentTransaction()
                ?? throw new InvalidOperationException(
                    "Cannot save an outbox event without an active transaction. " +
                    "Ensure AddAndSaveEventAsync is called within a TransactionBehavior scope.");

            await _eventLogService.SaveEventAsync(evt, transaction.TransactionId);
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
