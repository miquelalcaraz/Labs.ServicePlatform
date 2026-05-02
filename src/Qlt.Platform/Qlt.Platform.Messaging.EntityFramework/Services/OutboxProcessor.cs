using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Qlt.Platform.EventBus;
using Qlt.Platform.Messaging.Options;
using Qlt.Platform.Meters;
using Qlt.Platform.Persistence;

namespace Qlt.Platform.Messaging.Services;

/// <summary>
/// Background service that periodically polls the outbox table for unpublished or failed events
/// and attempts to publish them through the event bus.
/// Ensures reliable delivery of integration events when inline publishing fails or is skipped.
/// </summary>
public class OutboxProcessor : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly MigrationReadySignal _migrationReady;
    private readonly OutboxProcessorOptions _options;
    private readonly ILogger<OutboxProcessor> _logger;

    public OutboxProcessor(
        IServiceScopeFactory scopeFactory,
        MigrationReadySignal migrationReady,
        IOptions<OutboxProcessorOptions> options,
        ILogger<OutboxProcessor> logger)
    {
        _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
        _migrationReady = migrationReady ?? throw new ArgumentNullException(nameof(migrationReady));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await _migrationReady.WaitAsync(stoppingToken);

        _logger.LogInformation(
            "Outbox processor started. Polling interval: {PollingInterval}, batch size: {BatchSize}, max retries: {MaxRetries}",
            _options.PollingInterval,
            _options.BatchSize,
            _options.MaxRetryCount);

        using var timer = new PeriodicTimer(_options.PollingInterval);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessOutboxAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error during outbox processing cycle");
            }

            try
            {
                await timer.WaitForNextTickAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }

        _logger.LogInformation("Outbox processor stopped");
    }

    private async Task ProcessOutboxAsync(CancellationToken stoppingToken)
    {
        using var activity = ApplicationDiagnostics.ActivitySource.StartActivity(
            name: "Outbox Background Processing",
            kind: ActivityKind.Internal);

        var stopwatch = Stopwatch.StartNew();

        await using var scope = _scopeFactory.CreateAsyncScope();
        var outboxService = scope.ServiceProvider.GetRequiredService<IOutboxMessageService>();
        var eventBus = scope.ServiceProvider.GetRequiredService<IEventBus>();

        var pendingEvents = await outboxService.RetrieveUnpublishedEventsAsync(
            _options.BatchSize,
            _options.MaxRetryCount,
            stoppingToken);

        var eventsList = pendingEvents.ToList();

        if (eventsList.Count == 0)
        {
            return;
        }

        _logger.LogInformation("Outbox processor found {EventCount} pending events", eventsList.Count);

        ApplicationDiagnostics.OutboxBatchSize.Record(eventsList.Count);
        activity?.SetTag("outbox.processor.event_count", eventsList.Count);

        var successCount = 0;
        var failureCount = 0;

        foreach (var logEvt in eventsList)
        {
            if (stoppingToken.IsCancellationRequested)
                break;

            _logger.LogInformation(
                "Outbox processor publishing integration event: {IntegrationEventId} (attempt {Attempt})",
                logEvt.Id,
                logEvt.SendCount);

            try
            {
                await eventBus.PublishAsync(logEvt.DeserializedEvent!);
                await outboxService.MarkEventAsPublishedAsync(logEvt.Id);
                successCount++;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Outbox processor failed to publish integration event: {IntegrationEventId}", logEvt.Id);

                await outboxService.MarkEventAsFailedAsync(logEvt.Id);
                failureCount++;

                ApplicationDiagnostics.OutboxEventsFailed.Add(1,
                    new KeyValuePair<string, object?>("event_type", logEvt.DeserializedEvent?.GetType().Name ?? logEvt.TypeName));
            }
        }

        stopwatch.Stop();

        activity?.SetTag("outbox.processor.published_count", successCount);
        activity?.SetTag("outbox.processor.failed_count", failureCount);
        activity?.SetTag("outbox.processor.duration_ms", stopwatch.Elapsed.TotalMilliseconds);

        if (failureCount > 0)
        {
            activity?.SetStatus(ActivityStatusCode.Error, $"{failureCount} events failed to publish");
        }

        _logger.LogInformation(
            "Outbox processor completed cycle: {SuccessCount} published, {FailureCount} failed in {ElapsedMs}ms",
            successCount,
            failureCount,
            stopwatch.Elapsed.TotalMilliseconds);
    }
}
