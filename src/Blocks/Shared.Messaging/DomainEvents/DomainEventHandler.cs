using QltS.Shared.Telemetry.Activities;

using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace QltS.Shared.Messaging.DomainEvents;

/// <summary>
/// Base class for domain event handlers with built-in telemetry.
/// Automatically creates Activity spans and records metrics for each handler execution.
/// </summary>
/// <typeparam name="TDomainEvent">The domain event type</typeparam>
public abstract class DomainEventHandler<TDomainEvent> : IDomainEventHandler<TDomainEvent>
    where TDomainEvent : IDomainEvent
{
    /// <summary>
    /// Handles the domain event with automatic telemetry.
    /// Do not override this method - override HandleAsync instead.
    /// </summary>
    public async Task Handle(TDomainEvent notification, CancellationToken cancellationToken)
    {
        var eventType = typeof(TDomainEvent).Name;
        var handlerType = GetType().Name;

        // Start activity for distributed tracing
        using var activity = ActivityHelper.StartHandlerActivity(
            activityName: $"Handle {eventType}");

        ActivityHelper.SetHandlerTags(activity, eventType, handlerType, "event");
        ActivityHelper.SetDomainEventTag(activity);

        var stopwatch = Stopwatch.StartNew();

        try
        {
            await HandleAsync(notification, cancellationToken);
            stopwatch.Stop();

            var elapsedMs = stopwatch.Elapsed.TotalMilliseconds;

            // Record successful processing metric
            MetricsHelper.RecordDomainEventHandlerExecution(eventType, handlerType, elapsedMs, success: true);

            ActivityHelper.RecordHandlerSuccess(activity, elapsedMs, success: true);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            var elapsedMs = stopwatch.Elapsed.TotalMilliseconds;

            // Record failed processing metric
            MetricsHelper.RecordDomainEventHandlerExecution(eventType, handlerType, elapsedMs, success: false);

            ActivityHelper.RecordHandlerSuccess(activity, elapsedMs, success: false);
            ActivityHelper.RecordException(activity, ex);

            throw;
        }
    }

    /// <summary>
    /// Override this method to implement your domain event handling logic.
    /// Telemetry (activities and metrics) will be automatically recorded by the base class.
    /// </summary>
    /// <param name="notification">The domain event to handle</param>
    /// <param name="cancellationToken">Cancellation token</param>
    protected abstract Task HandleAsync(TDomainEvent notification, CancellationToken cancellationToken);
}
