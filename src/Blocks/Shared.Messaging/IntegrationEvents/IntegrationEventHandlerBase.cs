using QltS.Shared.Telemetry.Activities;

using System;
using System.Diagnostics;
using System.Threading.Tasks;





namespace QltS.Shared.Messaging.IntegrationEvents;

/// <summary>
/// Base class for integration event handlers with built-in telemetry.
/// Automatically creates Activity spans and records metrics for each handler execution.
/// </summary>
/// <typeparam name="TIntegrationEvent">The integration event type</typeparam>
public abstract class IntegrationEventHandlerBase<TIntegrationEvent> : IIntegrationEventHandler<TIntegrationEvent>
    where TIntegrationEvent : IntegrationEvent
{


    /// <summary>
    /// Handles the integration event with automatic telemetry.
    /// Do not override this method - override HandleAsync instead.
    /// </summary>
    public async Task Handle(TIntegrationEvent @event)
    {
        var eventType = typeof(TIntegrationEvent).Name;
        var handlerType = GetType().Name;

        // Start activity for distributed tracing
        using var activity = ActivityHelper.StartHandlerActivity(
            activityName: $"Handle {eventType}",
            kind: ActivityKind.Consumer);

        ActivityHelper.SetHandlerTags(activity, eventType, handlerType, "event");
        ActivityHelper.SetEventTags(activity, @event.Id, @event.CreationDate);

        var stopwatch = Stopwatch.StartNew();

        try
        {
            await HandleAsync(@event);
            stopwatch.Stop();

            var elapsedMs = stopwatch.Elapsed.TotalMilliseconds;

            // Record successful processing metric
            MetricsHelper.RecordIntegrationEventHandlerExecution(eventType, handlerType, elapsedMs, success: true);

            ActivityHelper.RecordHandlerSuccess(activity, elapsedMs, success: true);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            var elapsedMs = stopwatch.Elapsed.TotalMilliseconds;

            // Record failed processing metric
            MetricsHelper.RecordIntegrationEventHandlerExecution(eventType, handlerType, elapsedMs, success: false);

            ActivityHelper.RecordHandlerSuccess(activity, elapsedMs, success: false);
            ActivityHelper.RecordException(activity, ex);

            throw;
        }
    }

    /// <summary>
    /// Override this method to implement your integration event handling logic.
    /// Telemetry (activities and metrics) will be automatically recorded by the base class.
    /// </summary>
    /// <param name="event">The integration event to handle</param>
    protected abstract Task HandleAsync(TIntegrationEvent @event);
}
