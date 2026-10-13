using System;
using System.Diagnostics;
using System.Threading.Tasks;

using Sirius.Platform.Activities;

namespace Sirius.Platform.Messaging.IntegrationEvents;

/// <summary>
/// Decorator that automatically adds telemetry (OpenTelemetry activities and metrics) to integration event handlers.
/// Wraps any IIntegrationEventHandler and tracks execution time, success/failure, and exceptions.
/// This decorator should be applied OUTSIDE the idempotency decorator to measure total execution time.
/// </summary>
/// <typeparam name="TIntegrationEvent">The integration event type</typeparam>
public class TelemetryIntegrationEventHandlerDecorator<TIntegrationEvent> : IIntegrationEventHandler<TIntegrationEvent>, IDecoratedHandler<TIntegrationEvent>
    where TIntegrationEvent : IntegrationEvent
{
    private readonly IIntegrationEventHandler<TIntegrationEvent> _inner;

    public IIntegrationEventHandler<TIntegrationEvent> InnerHandler => _inner;

    public TelemetryIntegrationEventHandlerDecorator(IIntegrationEventHandler<TIntegrationEvent> inner)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
    }

    public async Task Handle(TIntegrationEvent @event)
    {
        var eventType = typeof(TIntegrationEvent).Name;
        var handlerType = GetActualHandlerName(_inner);

        // Start activity for distributed tracing
        using var activity = ActivityHelper.StartHandlerActivity(
            activityName: $"Handle {eventType}",
            kind: ActivityKind.Consumer);

        ActivityHelper.SetHandlerTags(activity, eventType, handlerType, "event");
        ActivityHelper.SetEventTags(activity, @event.Id, @event.CreationDate);

        var stopwatch = Stopwatch.StartNew();

        try
        {
            // Execute inner handler (which may be another decorator or the actual handler)
            await _inner.Handle(@event);

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

    private static string GetActualHandlerName(IIntegrationEventHandler<TIntegrationEvent> handler)
    {
        while (handler is IDecoratedHandler<TIntegrationEvent> decorator)
            handler = decorator.InnerHandler;

        return handler.GetType().Name;
    }
}
