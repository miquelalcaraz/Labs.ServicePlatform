using MediatR;
using Qlt.Domain;
using Qlt.Platform.Activities;

using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace Qlt.Platform.Messaging.DomainEvents;

/// <summary>
/// Decorator that adds telemetry (activities and metrics) to domain event handlers.
/// This decorator is automatically applied to all IDomainEventHandler implementations via DI.
/// </summary>
/// <typeparam name="TDomainEvent">The domain event type</typeparam>
public class TelemetryDomainEventHandlerDecorator<TDomainEvent> : IDomainEventHandler<TDomainEvent>
    where TDomainEvent : IDomainEvent, INotification
{
    private readonly IDomainEventHandler<TDomainEvent> _inner;

    public TelemetryDomainEventHandlerDecorator(IDomainEventHandler<TDomainEvent> inner)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
    }

    public async Task Handle(TDomainEvent notification, CancellationToken cancellationToken)
    {
        var eventType = typeof(TDomainEvent).Name;
        var handlerType = _inner.GetType().Name;

        // Start activity for distributed tracing
        using var activity = ActivityHelper.StartHandlerActivity(
            activityName: $"Handle {eventType}");

        ActivityHelper.SetHandlerTags(activity, eventType, handlerType, "domain-event");
        ActivityHelper.SetDomainEventTag(activity);

        var stopwatch = Stopwatch.StartNew();

        try
        {
            await _inner.Handle(notification, cancellationToken);
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
}
