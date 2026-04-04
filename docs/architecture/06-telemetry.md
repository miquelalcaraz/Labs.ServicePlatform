# Handler Telemetry Strategy

## Overview

Unified OpenTelemetry strategy for all handler types. Each handler type uses the correct MediatR extension point for its execution path, ensuring automatic distributed tracing and metrics with zero risk of bypass.

**Layer**: `Platform.Messaging`  
**Key types**: `TelemetryCommandBehavior<TRequest, TResponse>`, `TelemetryDomainEventBehavior`, `TelemetryIntegrationEventHandlerDecorator<T>`

---

## Strategy Per Handler Type

| Handler Type | MediatR Path | Extension Mechanism | Implementation |
|---|---|---|---|
| **Commands** | `Send()` | `IPipelineBehavior<,>` | `TelemetryCommandBehavior<,>` |
| **Domain Events** | `Publish()` | `INotificationPublisher` | `TelemetryDomainEventBehavior` |
| **Integration Events** | Outside MediatR | Scrutor `TryDecorate` | `TelemetryIntegrationEventHandlerDecorator<>` |

---

## MediatR Execution Paths

MediatR has two distinct paths with different extension mechanisms:

```
Send(command) → IPipelineBehavior₁ → IPipelineBehavior₂ → ... → IRequestHandler
                N behaviors chained automatically (Chain of Responsibility)

Publish(event) → INotificationPublisher.Publish(handlers[])
                 Single orchestrator (one registered instance)
```

`IPipelineBehavior` supports multiple behaviors composed automatically. `INotificationPublisher` is a single instance controlling all notification handler execution — it is **not chainable**.

---

## Commands — IPipelineBehavior

`TelemetryCommandBehavior<TRequest, TResponse>` wraps command execution with tracing and metrics:

```csharp
public class TelemetryCommandBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private static readonly bool IsCommand = DetectCommandType();

    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        if (!IsCommand)
            return await next(cancellationToken);

        // Activity + Stopwatch + MetricsHelper.RecordCommandHandlerExecution(...)
    }
}
```

Uses a static cached `bool IsCommand` to skip non-command requests at zero cost. `DetectCommandType()` checks for `ICommand` and `ICommand<>` at type initialization.

**Registration**: `services.AddTransient(typeof(IPipelineBehavior<,>), typeof(TelemetryCommandBehavior<,>));`

---

## Domain Events — INotificationPublisher

`TelemetryDomainEventBehavior` replaces MediatR's default `ForeachAwaitPublisher` to add telemetry around each domain event handler:

```csharp
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
                await ExecuteWithTelemetry(handler, notification, cancellationToken);
            else
                await handler.HandlerCallback(notification, cancellationToken);
        }
    }
}
```

Filters by `IDomainEventHandler<>` interface — only domain event handlers get telemetry. Other `INotificationHandler` registrations execute normally.

**Registration**: `services.AddSingleton<INotificationPublisher, TelemetryDomainEventBehavior>();`

**Order independence**: Works regardless of registration order relative to `AddMediatR()`. MediatR uses `TryAddSingleton` for its default publisher.

---

## Integration Events — Scrutor Decorator

Integration events are registered directly in DI via `eventBus.AddSubscription<TEvent, THandler>()` (not through MediatR), so Scrutor can find and decorate them:

```csharp
services.TryDecorate(typeof(IIntegrationEventHandler<>), typeof(TelemetryIntegrationEventHandlerDecorator<>));
```

---

## Why Not Scrutor for Everything?

| Factor | Scrutor `TryDecorate` | MediatR Native |
|---|---|---|
| **MediatR commands** | ❌ Fails silently (registered as `IRequestHandler`, not `ICommandHandler`) | ✅ `IPipelineBehavior` |
| **MediatR notifications** | ❌ Fails silently (registered as `INotificationHandler`, not `IDomainEventHandler`) | ✅ `INotificationPublisher` |
| **Non-MediatR handlers** | ✅ Works (registered directly in DI) | N/A |
| **MediatR version upgrades** | ❌ Depends on DI registration internals | ✅ Public API contract |

---

## Telemetry Data Captured

All three implementations record the same telemetry categories:

| Signal | Method | Data |
|--------|--------|------|
| Distributed Tracing | `ActivityHelper.StartHandlerActivity()` | Activity with handler type tags |
| Handler Tags | `ActivityHelper.SetHandlerTags()` | Event type, handler type, category |
| Execution Time | `Stopwatch` → `MetricsHelper.Record*Execution()` | Duration in ms, success/failure |
| Exceptions | `ActivityHelper.RecordException()` | Full exception on activity |
| Result Errors | `ActivityHelper.RecordResultError()` | Error code + message (commands only) |

---

## Registration

```csharp
services.AddIntegrationEventTelemetryDecorator();  // Scrutor — for non-MediatR handlers
services.AddDomainEventTelemetryBehavior();         // INotificationPublisher — for Publish() path
services.AddCommandTelemetryBehavior();             // IPipelineBehavior — for Send() path
```

---

## INotificationPublisher Constraint

Unlike `IPipelineBehavior` which supports N behaviors chained automatically, `INotificationPublisher` is a single orchestrator. If additional cross-cutting concerns are needed for domain events, they must be added inside the same `TelemetryDomainEventBehavior` class rather than registering multiple publishers.

---

## Validation Checklist

- [ ] `TelemetryCommandBehavior` registered as `IPipelineBehavior<,>` in all services
- [ ] `TelemetryDomainEventBehavior` registered as `INotificationPublisher` in all services
- [ ] `TelemetryIntegrationEventHandlerDecorator` registered via Scrutor in all services
- [ ] No Scrutor `TryDecorate` targeting `ICommandHandler<,>` or `IDomainEventHandler<>`
