# Developer Guide: Service Configuration

## Overview

This guide explains how to configure services in the QltS platform following the **two-layer pattern**. The platform provides `AddQltMediator` to register the MediatR pipeline with automatic telemetry, eliminating manual behavior ordering and telemetry registration.

---

## Two-Layer Configuration

Every service splits its DI configuration into two layers:

```
Layer 1 — Application (Api/Extensions)
  ├─ AddQltMediator (handlers + pipeline)
  ├─ AddValidatorsFromAssembly
  └─ AddHttpContextAccessor

         calls ↓

Layer 2 — Infrastructure (Infrastructure/Extensions)
  ├─ AddQltSDBContext<T>          (database)
  ├─ AddQltReadDbContext<T>       (read-only database)
  ├─ AddServiceBusEventBus        (event bus)
  ├─ AddIdempotencyEntityFramework<T>
  ├─ AddRedisIdempotencyCache
  ├─ AddTransactionalOutbox<T>    (outbox)
  └─ Repositories
```

---

## AddQltMediator

Single entry point for the MediatR pipeline. Replaces manual `AddMediatR` + behavior registration + telemetry calls.

### What it registers automatically

- `IdempotencyBehavior`
- `IntegrationEventProcessorBehavior`
- `TransactionBehavior`
- `DomainEventProcessorBehavior`
- `TelemetryCommandBehavior`, `TelemetryDomainEventBehavior` (via `INotificationPublisher`), `TelemetryIntegrationEventHandlerDecorator`

### What the developer configures

| Method | Required | Purpose |
|--------|----------|---------|
| `RegisterServicesFromAssembly(assembly)` | Yes | MediatR handler discovery |
| `WithLogging(typeof(LoggingBehavior<,>))` | Optional | Logging at the outermost position |
| `WithValidation(typeof(ValidationBehavior<,>))` | Optional | Validation inside the transaction |

### Pipeline order (managed by platform)

```
Logging (optional)
  └─ Idempotency (automatic)
       └─ IntegrationEventProcessor (automatic — OUTSIDE transaction)
            └─ Transaction (automatic — BEGIN/COMMIT)
                 └─ Validation (optional — INSIDE transaction)
                      └─ DomainEventProcessor (automatic — INSIDE transaction)
                           └─ CommandHandler
```

All platform behaviors degrade gracefully when their dependencies are not registered (pass-through).

---

## Complete Example: Accounts Service

### Layer 1 — Application

```csharp
public static IServiceCollection AddApplicationServices(this IHostApplicationBuilder builder)
{
    var services = builder.Services;

    builder.AddInfrastructureServices();
    builder.AddRedisClient("cache");

    services.AddHttpContextAccessor();

    services.AddQltMediator(cfg =>
    {
        cfg.RegisterServicesFromAssembly(AssemblyReference.Assembly);
        cfg.RegisterServicesFromAssembly(typeof(Infrastructure.AssemblyReference).Assembly);
        cfg.WithLogging(typeof(LoggingBehavior<,>));
        cfg.WithValidation(typeof(ValidationBehavior<,>));
    });

    services.AddValidatorsFromAssembly(AssemblyReference.Assembly);

    return services;
}
```

### Layer 2 — Infrastructure

```csharp
public static void AddInfrastructureServices(this IHostApplicationBuilder builder)
{
    var services = builder.Services;
    var connectionString = builder.Configuration.GetConnectionString("accountdb");
    var readConnectionString = builder.Configuration.GetConnectionString("accountdb-readonly")
        ?? connectionString;

    services.AddQltSDBContext<AccountContext>(
        conf => conf
            .SetDefaultSchema("accounts")
            .WithIdempotency()
            .RegisterModelsFromAssembly(typeof(AssemblyReference).Assembly),
        options => options.UseSqlServer(connectionString, sqlOptions =>
        {
            sqlOptions.MigrationsAssembly(typeof(AssemblyReference).Assembly.FullName);
        })
    );

    services.AddQltReadDbContext<AccountReadContext>(
        conf => conf
            .SetDefaultSchema("accounts")
            .RegisterModelsFromAssembly(typeof(AssemblyReference).Assembly),
        options => options.UseSqlServer(readConnectionString)
    );

    services.AddMigration<AccountContext, QltDBContextSeed>();

    builder.AddServiceBusEventBus("eventbus")
        .AddEventBusSubscriptions();

    services.AddRedisIdempotencyCache(builder.Configuration)
        .ForApiRequests()
        .ForIntegrationEventHandlers();

    services.AddIdempotencyEntityFramework<AccountContext>()
        .ForApiRequests()
        .ForIntegrationEventHandlers();

    services.AddTransactionalOutbox<AccountContext>(options =>
    {
        options.RegisterEventTypes(typeof(Contracts.AssemblyReference).Assembly);
    });

    services.AddScoped<IOrganizationRepository, OrganizationRepository>();
}
```

---

## Infrastructure Component Reference

### Database

```csharp
services.AddQltSDBContext<MyContext>(
    conf => conf
        .SetDefaultSchema("myschema")
        .WithIdempotency()
        .WithAuditLogs()
        .RegisterModelsFromAssembly(typeof(AssemblyReference).Assembly),
    options => options.UseSqlServer(connectionString)
);
```

### Read-Only Database

```csharp
services.AddQltReadDbContext<MyReadContext>(
    conf => conf
        .SetDefaultSchema("myschema")
        .RegisterModelsFromAssembly(typeof(AssemblyReference).Assembly),
    options => options.UseSqlServer(readConnectionString)
);
```

### Idempotency

```csharp
// Redis + EF (recommended)
services.AddRedisIdempotencyCache(builder.Configuration)
    .ForApiRequests()
    .ForIntegrationEventHandlers();

services.AddIdempotencyEntityFramework<MyContext>()
    .ForApiRequests()
    .ForIntegrationEventHandlers();

// EF only
services.AddIdempotencyEntityFramework<MyContext>()
    .ForIntegrationEventHandlers();
```

| Method | Effect |
|--------|--------|
| `.ForApiRequests()` | Enables `IdempotencyBehavior` for MediatR commands |
| `.ForIntegrationEventHandlers()` | Decorates `IIntegrationEventHandler<>` with idempotent consumer |

When both Redis and EF are registered, EF decorates Redis — Redis as fast cache, EF as transactional guarantee.

### Transactional Outbox

```csharp
services.AddTransactionalOutbox<MyContext>(options =>
{
    options.RegisterEventTypes(typeof(Contracts.AssemblyReference).Assembly);
});
```

### Event Bus

```csharp
builder.AddServiceBusEventBus("eventbus")
    .AddSubscription<SomeIntegrationEvent, SomeIntegrationEventHandler>();
```

---

## New Service Checklist

- [ ] Create `Api/Extensions/Extensions.cs` with `AddApplicationServices`
- [ ] Create `Infrastructure/Extensions/Extensions.cs` with `AddInfrastructureServices`
- [ ] Call `builder.AddInfrastructureServices()` from the application layer
- [ ] Use `AddQltMediator` — do NOT call `AddMediatR` directly
- [ ] Register handlers from all relevant assemblies via `RegisterServicesFromAssembly`
- [ ] Add `WithLogging` / `WithValidation` as needed
- [ ] Configure `AddQltSDBContext<T>` with `.WithIdempotency()` if using idempotency
- [ ] Configure `AddTransactionalOutbox<T>` with the contracts assembly
- [ ] Do NOT manually register telemetry behaviors — they are implicit

---

## Common Mistakes

### Calling AddMediatR directly

```csharp
// ❌ Bypasses platform pipeline and telemetry
services.AddMediatR(cfg => { cfg.RegisterServicesFromAssembly(assembly); });

// ✅ Platform manages pipeline order and telemetry
services.AddQltMediator(cfg => { cfg.RegisterServicesFromAssembly(assembly); });
```

### Manually registering telemetry

```csharp
// ❌ No longer needed — implicit in AddQltMediator
services.AddIntegrationEventTelemetryDecorator();
services.AddDomainEventTelemetryBehavior();
```

### Mixing application and infrastructure concerns

```csharp
// ❌ Mixed in one method
public static void AddApplicationServices(this IHostApplicationBuilder builder)
{
    services.AddQltSDBContext<MyContext>(...);    // infrastructure
    services.AddQltMediator(cfg => { ... });     // application
}

// ✅ Clear separation
public static void AddApplicationServices(this IHostApplicationBuilder builder)
{
    builder.AddInfrastructureServices();          // delegates infrastructure
    services.AddQltMediator(cfg => { ... });      // application only
}
```
