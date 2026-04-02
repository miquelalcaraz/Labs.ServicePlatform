# Handler Telemetry Strategy

## Executive Summary

Unified telemetry strategy for all handler types using MediatR's native extension points instead of Scrutor decorators. Each handler type uses the correct interception mechanism for its MediatR execution path.

**Status**: ✅ Implementation complete and validated  
**Impact**: Automatic OpenTelemetry tracing and metrics for all handler executions  
**Architecture**: Zero coupling to DI container internals; uses MediatR's public API exclusively

---

## Problem Statement

The original approach used Scrutor's `TryDecorate` to wrap handlers with telemetry decorators. This failed silently for MediatR-managed handlers because of a fundamental mismatch:

```
MediatR registers handlers as:
  Commands     → IRequestHandler<TCommand, TResponse>
  Domain Events → INotificationHandler<TEvent>

Scrutor TryDecorate targeted:
  Commands     → ICommandHandler<,>        ← NOT what MediatR registered
  Domain Events → IDomainEventHandler<>     ← NOT what MediatR registered

Result: TryDecorate found zero matching service descriptors → returned false silently → no telemetry
```

**Root cause**: MediatR registers handlers under its own base interfaces (`IRequestHandler<,>`, `INotificationHandler<>`), not under derived project-specific interfaces (`ICommandHandler<,>`, `IDomainEventHandler<>`). Scrutor searches service descriptors by exact `ServiceType` match.

**Note**: Integration event handlers are registered directly in DI (via `eventBus.AddSubscription<TEvent, THandler>()`), not through MediatR. Scrutor `TryDecorate` works correctly for them.

---

## Solution Architecture

### MediatR Execution Paths

MediatR has two distinct execution paths with different extension mechanisms:

```
Send(command) → IPipelineBehavior₁ → IPipelineBehavior₂ → ... → IRequestHandler
                ^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^
                N behaviors composed automatically (Chain of Responsibility)

Publish(event) → INotificationPublisher.Publish(handlers[])
                 ^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^
                 Single orchestrator (one registered instance)
                      ├── INotificationHandler₁
                      ├── INotificationHandler₂
                      └── INotificationHandler₃
```

Key difference: `IPipelineBehavior` supports multiple behaviors chained automatically. `INotificationPublisher` is a single instance that controls how all notification handlers execute.

### Strategy Per Handler Type

| Handler Type | MediatR Path | Extension Mechanism | Implementation |
|---|---|---|---|
| **Commands** | `Send()` | `IPipelineBehavior<,>` | `TelemetryCommandBehavior<,>` |
| **Domain Events** | `Publish()` | `INotificationPublisher` | `TelemetryDomainEventBehavior` |
| **Integration Events** | Outside MediatR | Scrutor `TryDecorate` | `TelemetryIntegrationEventHandlerDecorator<>` |

---

## Technical Implementation

### Location

- Command Telemetry: `src/Qlt.Platform/Qlt.Platform.Messaging/Behaviors/TelemetryCommandBehavior.cs`
- Domain Event Telemetry: `src/Qlt.Platform/Qlt.Platform.Messaging/Behaviors/TelemetryDomainEventBehavior.cs`
- Integration Event Telemetry: `src/Qlt.Platform/Qlt.Platform.Messaging/IntegrationEvents/TelemetryIntegrationEventHandlerDecorator.cs`
- Registration: `src/Qlt.Platform/Qlt.Platform.Messaging/Extensions/ServiceCollectionExtensions.cs`

### 1. Commands — `IPipelineBehavior<TRequest, TResponse>`

MediatR resolves all registered `IPipelineBehavior<,>` and chains them into a pipeline around `IRequestHandler`. This is MediatR's native mechanism for request cross-cutting concerns.

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

**Key design**: Uses a static cached `bool IsCommand` to skip non-command requests at zero cost. `DetectCommandType()` checks for `ICommand` and `ICommand<>` at type initialization.

**Registration**:
```csharp
services.AddTransient(typeof(IPipelineBehavior<,>), typeof(TelemetryCommandBehavior<,>));
```

### 2. Domain Events — `INotificationPublisher`

MediatR resolves a single `INotificationPublisher` that controls how notification handlers execute. The default is `ForeachAwaitPublisher` (sequential, no telemetry). Our implementation replaces it.

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

**Key design**: Filters by `IDomainEventHandler<>` interface — only domain event handlers get telemetry. Other `INotificationHandler` registrations execute normally without overhead.

**Registration**:
```csharp
services.AddSingleton<INotificationPublisher, TelemetryDomainEventBehavior>();
```

**Registration order**: Works regardless of order relative to `AddMediatR()`. MediatR uses `TryAddSingleton` for its default publisher — if ours is registered first, MediatR skips its default. If registered after, ours is the last registration and wins with `GetRequiredService<T>()`.

### 3. Integration Events — Scrutor `TryDecorate`

Integration events are registered directly in DI via `eventBus.AddSubscription<TEvent, THandler>()`, not through MediatR. This means Scrutor can find and decorate them reliably.

```csharp
services.TryDecorate(typeof(IIntegrationEventHandler<>), (inner, serviceProvider) =>
{
    var eventType = inner.GetType().GetInterfaces()
        .FirstOrDefault(i => i.IsGenericType 
            && i.GetGenericTypeDefinition() == typeof(IIntegrationEventHandler<>))
        ?.GetGenericArguments()[0];

    var decoratorType = typeof(TelemetryIntegrationEventHandlerDecorator<>).MakeGenericType(eventType);
    return Activator.CreateInstance(decoratorType, inner)!;
});
```

---

## Registration Pattern

All telemetry is registered via extension methods in `ServiceCollectionExtensions`:

```csharp
services.AddIntegrationEventTelemetryDecorator();  // Scrutor — for non-MediatR handlers
services.AddDomainEventTelemetryBehavior();         // INotificationPublisher — for Publish() path
services.AddCommandTelemetryBehavior();             // IPipelineBehavior — for Send() path
```

---

## Why Not Scrutor For Everything?

| Factor | Scrutor `TryDecorate` | MediatR Native |
|---|---|---|
| **MediatR commands** | ❌ Fails silently (registers as `IRequestHandler`, not `ICommandHandler`) | ✅ `IPipelineBehavior` |
| **MediatR notifications** | ❌ Fails silently (registers as `INotificationHandler`, not `IDomainEventHandler`) | ✅ `INotificationPublisher` |
| **Non-MediatR handlers** | ✅ Works (registered directly in DI) | N/A |
| **Registration order** | ⚠️ Must run after handler registration | ✅ Order-independent |
| **MediatR version upgrades** | ❌ Depends on DI registration internals | ✅ Public API contract |

---

## INotificationPublisher Constraints

Unlike `IPipelineBehavior` which supports N behaviors chained automatically, `INotificationPublisher` is a **single orchestrator**:

```
IPipelineBehavior:      Logging → Validation → Idempotency → Transaction → Handler
                        (N behaviors, composed automatically by MediatR)

INotificationPublisher: Single instance controls ALL notification handler execution
                        (one publisher, not chainable)
```

If additional cross-cutting concerns are needed for domain events, they should be added inside the same `TelemetryDomainEventBehavior` class rather than registering multiple publishers.

---

## Telemetry Data Captured

All three implementations record the same telemetry categories:

| Telemetry | Method | Data |
|---|---|---|
| **Distributed Tracing** | `ActivityHelper.StartHandlerActivity()` | Activity with handler type tags |
| **Handler Tags** | `ActivityHelper.SetHandlerTags()` | Event type, handler type, category |
| **Execution Time** | `Stopwatch` → `MetricsHelper.Record*Execution()` | Duration in ms, success/failure |
| **Exceptions** | `ActivityHelper.RecordException()` | Full exception on activity |
| **Result Errors** | `ActivityHelper.RecordResultError()` | Error code + message (commands only) |

---

## Validation Checklist

- [ ] `TelemetryCommandBehavior` registered as `IPipelineBehavior<,>` in all services
- [ ] `TelemetryDomainEventBehavior` registered as `INotificationPublisher` in all services
- [ ] `TelemetryIntegrationEventHandlerDecorator` registered via Scrutor in all services
- [ ] No Scrutor `TryDecorate` targeting `ICommandHandler<,>` or `IDomainEventHandler<>`
- [ ] No `TelemetryDomainEventHandlerDecorator` references remaining (replaced by behavior)
