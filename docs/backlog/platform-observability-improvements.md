# Platform Observability — Proposed Improvements

> **Status**: Proposed — deferred to keep the platform scope focused.
> **Related**: [06 — Telemetry](../architecture/06-telemetry.md), [07 — Event Bus](../architecture/07-event-bus.md)

Two cross-cutting improvements that belong in the base platform rather than in each service. Both are independent of any feature and apply to every service built on `Sirius.Platform`.

---

## 1. Automatic DB Health Check in `AddSiriusSDBContext<TContext>`

### Problem

`AddSiriusSDBContext<TContext>` registers the `DbContext` via plain `services.AddDbContext<TContext>`, bypassing the Aspire enrollment that auto-registers health checks. Services that use this method have no database readiness probe unless they add one manually.

Today both Accounts and Notifications repeat this boilerplate after calling `AddSiriusSDBContext`:

```csharp
services.AddHealthChecks()
    .AddDbContextCheck<AccountContext>(name: "accounts-db", tags: ["ready"]);
```

Omitting it makes `/health/ready` report `Healthy` even when the database is unreachable — a silent misconfiguration.

### Proposed Design

Register the health check inside `AddSiriusSDBContext<TContext>` in `Platform.Persistence.EntityFramework.Relational`:

```csharp
public static IServiceCollection AddSiriusSDBContext<TContext>(
    this IServiceCollection services,
    Action<SiriusDbContextConfiguration> configuration,
    Action<DbContextOptionsBuilder> dbOptions)
    where TContext : SiriusDBContext
{
    // ... existing registration ...

    services.AddHealthChecks()
        .AddDbContextCheck<TContext>(
            name: typeof(TContext).Name.ToLowerKebabCase(),
            tags: ["ready"]);

    return services;
}
```

The name defaults to the kebab-cased context type name (`account-context`, `notifications-context`), so it is self-describing in health dashboards without any caller configuration.

### Dependency

`Microsoft.Extensions.Diagnostics.HealthChecks.EntityFrameworkCore` must be added to `Platform.Persistence.EntityFramework.Relational.csproj`.

### Impact

| Service | Current state | After |
|---------|--------------|-------|
| Accounts, Notifications | Manual `AddDbContextCheck` in each service | Removed — covered by the platform |
| Future services | Must remember to add it | Automatic |

---

## 2. Correlation-Aware Log Scope for Integration Event Handlers

### Problem

The onboarding and activation workflows carry a `CorrelationId` across services, but only as an ad-hoc property: some integration events declare it (`ActivationEmailRequested`, `UserActivated`, `ActivationDelivered`, `CustomerGoLive`, `CustomerActivationCompleted`, all as `Guid?`), while the `IntegrationEvent` base record only has `Id` and `CreationDate`.

As a result, the platform cannot enrich logs with the correlation id generically. Command handlers open the log scope themselves after loading the process aggregate:

```csharp
using (_logger.BeginScope(new Dictionary<string, object?>
{
    ["CorrelationId"] = process.CorrelationId,
    ["ActivationProcessId"] = process.Id
}))
{
    // handler body
}
```

and integration event handlers do not open any scope, so their log entries cannot be correlated across services in a structured log store (Application Insights, Seq, etc.) without extra work.

### Proposed Design

**Step 1 — Make correlation a platform concept.** Add an opt-in contract in `Platform.Messaging.Abstractions` and implement it on the events that already carry the property:

```csharp
public interface ICorrelatedIntegrationEvent
{
    Guid? CorrelationId { get; }
}

public record ActivationEmailRequestedIntegrationEvent(
    /* ... */
    Guid? CorrelationId) : IntegrationEvent, ICorrelatedIntegrationEvent;
```

An opt-in interface keeps the change non-breaking; moving `CorrelationId` to the base record is the alternative if every event should carry it.

**Step 2 — Add a decorator** in `Platform.Messaging`, following the same pattern as `TelemetryIntegrationEventHandlerDecorator<T>` (including `IDecoratedHandler<T>`, so the idempotency decorator still resolves the real handler name):

```csharp
public class CorrelationLoggingIntegrationEventDecorator<T>
    : IIntegrationEventHandler<T>, IDecoratedHandler<T>
    where T : IntegrationEvent
{
    private readonly IIntegrationEventHandler<T> _inner;
    private readonly ILogger<CorrelationLoggingIntegrationEventDecorator<T>> _logger;

    public CorrelationLoggingIntegrationEventDecorator(
        IIntegrationEventHandler<T> inner,
        ILogger<CorrelationLoggingIntegrationEventDecorator<T>> logger)
    {
        _inner = inner;
        _logger = logger;
    }

    public IIntegrationEventHandler<T> InnerHandler => _inner;

    public async Task Handle(T @event)
    {
        var scope = new Dictionary<string, object?> { ["IntegrationEventId"] = @event.Id };

        if (@event is ICorrelatedIntegrationEvent { CorrelationId: { } correlationId })
            scope["CorrelationId"] = correlationId;

        using (_logger.BeginScope(scope))
        {
            await _inner.Handle(@event);
        }
    }
}
```

It is registered with Scrutor next to the telemetry decorator, so every handler gets the scope without code changes.

### Scope Limitation

The decorator only adds what is available on the event itself. Context that comes from loading an aggregate (such as `ActivationProcessId`) remains the responsibility of the command handler.

### Layer Compliance

`Platform.Messaging` does not reference Entity Framework. The decorator only uses `ILogger` and the messaging abstractions already present in the layer.

---

## Summary

| Improvement | Layer | Mechanism |
|-------------|-------|-----------|
| Automatic DB health check | `Platform.Persistence.EntityFramework.Relational` | `AddDbContextCheck<TContext>` inside `AddSiriusSDBContext` |
| Correlation log scope | `Platform.Messaging.Abstractions` + `Platform.Messaging` | `ICorrelatedIntegrationEvent` + `CorrelationLoggingIntegrationEventDecorator<T>` via Scrutor |

Both follow the existing architectural patterns of the platform and introduce no breaking changes.
