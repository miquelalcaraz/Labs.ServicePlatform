using MediatR;

using Qlt.Platform.Activities;
using Qlt.Platform.Messaging.DomainEvents;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Qlt.Platform.Messaging.Behaviors;

/// <summary>
/// Notification publisher that adds telemetry (OpenTelemetry activities and metrics) to domain event handlers.
/// Replaces MediatR's default ForeachAwaitPublisher, maintaining sequential execution while adding
/// distributed tracing and performance metrics for handlers implementing IDomainEventHandler.
/// Only activates for handlers implementing IDomainEventHandler&lt;T&gt;; other notification handlers execute without telemetry.
/// </summary>
public class TelemetryDomainEventBehavior : INotificationPublisher
{
    public async Task Publish(
        IEnumerable<NotificationHandlerExecutor> handlerExecutors,
        INotification notification,
        CancellationToken cancellationToken)
    {
        foreach (var handler in handlerExecutors)
        {
            if (IsDomainEventHandler(handler.HandlerInstance))
            {
                await ExecuteWithTelemetry(handler, notification, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                await handler.HandlerCallback(notification, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private static async Task ExecuteWithTelemetry(
        NotificationHandlerExecutor handler,
        INotification notification,
        CancellationToken cancellationToken)
    {
        var eventType = notification.GetType().Name;
        var handlerType = handler.HandlerInstance.GetType().Name;

        using var activity = ActivityHelper.StartHandlerActivity(
            activityName: $"Handle {eventType}");

        ActivityHelper.SetHandlerTags(activity, eventType, handlerType, "domain-event");
        ActivityHelper.SetDomainEventTag(activity);

        var stopwatch = Stopwatch.StartNew();

        try
        {
            await handler.HandlerCallback(notification, cancellationToken).ConfigureAwait(false);
            stopwatch.Stop();

            var elapsedMs = stopwatch.Elapsed.TotalMilliseconds;

            MetricsHelper.RecordDomainEventHandlerExecution(eventType, handlerType, elapsedMs, success: true);
            ActivityHelper.RecordHandlerSuccess(activity, elapsedMs, success: true);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            var elapsedMs = stopwatch.Elapsed.TotalMilliseconds;

            MetricsHelper.RecordDomainEventHandlerExecution(eventType, handlerType, elapsedMs, success: false);
            ActivityHelper.RecordHandlerSuccess(activity, elapsedMs, success: false);
            ActivityHelper.RecordException(activity, ex);

            throw;
        }
    }

    private static bool IsDomainEventHandler(object handlerInstance)
    {
        return handlerInstance.GetType().GetInterfaces()
            .Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IDomainEventHandler<>));
    }
}
