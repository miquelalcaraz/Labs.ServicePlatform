# Platform Observability — Proposed Improvements

> **Status**: Proposed — Deferred to avoid scope creep. Applicable to all services built on `Qlt.Platform`.
> **Date**: 2026-04-10
> **Related**: [06 — Telemetry](../architecture/06-telemetry.md), [07 — Event Bus](../architecture/07-event-bus.md)

Two cross-cutting improvements identified as missing in the base platform. Both are independent of any feature and apply to all services built on `Qlt.Platform`.

---

## 1. Automatic DB Health Check in `AddQltSDBContext<TContext>`

### Problem

`AddQltSDBContext<TContext>` registers the `DbContext` via plain `services.AddDbContext<TContext>`, bypassing the Aspire enrollment that auto-registers health checks. Services that use this method have no SQL Server readiness probe unless they manually add one.

In the current state every service must write this boilerplate after calling `AddQltSDBContext`:

```csharp
services.AddHealthChecks()
    .AddDbContextCheck<MyContext>(name: "my-db", tags: ["ready"]);
```

Omitting it causes the `/health/ready` endpoint to return `Healthy` even when the database is unreachable — silent misconfiguration in production.

### Proposed Design

Embed the health check registration directly inside `AddQltSDBContext<TContext>` in `Platform.Persistence.EntityFramework.Relational`:

```csharp
// Platform.Persistence.EntityFramework.Relational/Builders/Extensions.cs
public static IServiceCollection AddQltSDBContext<TContext>(
    this IServiceCollection services,
    Action<QltDbContextConfiguration> configure,
    Action<DbContextOptionsBuilder> optionsAction)
    where TContext : DbContext
{
    services.AddDbContext<TContext>(optionsAction);

    services.AddHealthChecks()
        .AddDbContextCheck<TContext>(
            name: typeof(TContext).Name.ToLowerKebabCase(),
            tags: ["ready"]);

    return services;
}
```

The `name` defaults to a kebab-cased context type name (`account-context`, `notifications-context`) so it is self-describing in health check dashboards without any caller configuration.

### Dependency

`Microsoft.Extensions.Diagnostics.HealthChecks.EntityFrameworkCore` must be added to `Platform.Persistence.EntityFramework.Relational.csproj`. No other projects need the explicit reference.

### Effort

~1 hour (extension change + unit test + verify zero boilerplate in Accounts/Notifications).

### Impact

| Service | Current state | After |
|---------|--------------|-------|
| Services using `AddQltSDBContext<TContext>` with manual `AddDbContextCheck` | Boilerplate repeated in every service | Removed — covered by platform |
| All future services | Must remember to add it | Zero effort — automatic |

---

## 2. `CorrelationIdLoggingIntegrationEventDecorator<T>` in `Platform.Messaging`

### Problem

`IntegrationEvent` base class already carries a `CorrelationId` property. However, log entries emitted inside integration event handlers have no automatic log scope enrichment, making it impossible to correlate logs across services using structured log aggregators (Application Insights, Seq, etc.) without per-handler boilerplate:

```csharp
// Every handler must repeat this manually today
using (_logger.BeginScope(new Dictionary<string, object?>
{
    ["CorrelationId"] = @event.CorrelationId
}))
{
    // handler body
}
```

### Proposed Design

Add a new decorator in `Platform.Messaging` following the same pattern as `TelemetryIntegrationEventHandlerDecorator<T>`:

```csharp
// Platform.Messaging/Decorators/CorrelationIdLoggingIntegrationEventDecorator.cs
public class CorrelationIdLoggingIntegrationEventDecorator<T>
    : IIntegrationEventHandler<T>
    where T : IntegrationEvent
{
    private readonly IIntegrationEventHandler<T> _inner;
    private readonly ILogger<CorrelationIdLoggingIntegrationEventDecorator<T>> _logger;

    public CorrelationIdLoggingIntegrationEventDecorator(
        IIntegrationEventHandler<T> inner,
        ILogger<CorrelationIdLoggingIntegrationEventDecorator<T>> logger)
    {
        _inner = inner;
        _logger = logger;
    }

    public async Task Handle(T @event)
    {
        using (_logger.BeginScope(new Dictionary<string, object?>
        {
            ["CorrelationId"] = @event.CorrelationId
        }))
        {
            await _inner.Handle(@event);
        }
    }
}
```

Registration via Scrutor in `AddEventBus()` / `AddServiceBusEventBus()`, exactly as `TelemetryIntegrationEventHandlerDecorator<T>` is already registered:

```csharp
services.Decorate(
    typeof(IIntegrationEventHandler<>),
    typeof(CorrelationIdLoggingIntegrationEventDecorator<>));
```

### Scope Limitation

The decorator injects only what is available on `IntegrationEvent` base (`CorrelationId`, `EventId`, `CreationDate`). Handler-specific context fields — such as `ActivationProcessId` in Epic 2 handlers — remain the responsibility of each handler, as they depend on data fetched from the repository, not from the event itself.

### Layer Compliance

`Platform.Messaging` does NOT reference Entity Framework. This decorator uses only `ILogger` and the `IntegrationEvent` base type — both already present in the layer. No dependency violations.

### Effort

~2 hours (decorator class + Scrutor wiring + unit tests for scope injection + verify handlers can remove manual BeginScope for CorrelationId).

### Impact

| Handler | Current state | After |
|---------|--------------|-------|
| Handlers with manual `BeginScope(CorrelationId)` | `CorrelationId` scope setup repeated per handler | Decorator covers `CorrelationId`; only handler-specific scopes remain |
| All future handlers | Must remember to add it | Zero effort — automatic |

---

## Summary

| Improvement | Layer | Mechanism | Effort |
|-------------|-------|-----------|--------|
| Auto DB health check | `Platform.Persistence.EntityFramework.Relational` | Embed `AddDbContextCheck<TContext>` inside `AddQltSDBContext` | ~1h |
| Auto CorrelationId log scope | `Platform.Messaging` | New `CorrelationIdLoggingIntegrationEventDecorator<T>` via Scrutor | ~2h |

Both improvements follow the existing architectural patterns of the platform and introduce no breaking changes. Services implementing these capabilities manually today will simply have redundant-but-harmless registrations until they are cleaned up.
