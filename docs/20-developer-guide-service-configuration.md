# Developer Guide: Service Configuration

## Overview

This guide explains how to configure services in the QltS platform following the **two-layer pattern**. The platform provides `AddQltMediator` to register the MediatR pipeline with automatic telemetry, eliminating manual behavior ordering and telemetry registration.

**Status**: ✅ Standard pattern for all services  
**Applies to**: All microservices (`Qlt.Services.*`), infrastructure projects (`Qlt.Infrastructure.*`)

---

## Two-Layer Configuration

Every service splits its DI configuration into two layers with clear responsibilities:

```
┌─────────────────────────────────────────────────┐
│  Layer 1 — Application (Api/Extensions)         │
│  ┌───────────────────────────────────────────┐  │
│  │ AddQltMediator (handlers + pipeline)      │  │
│  │ AddValidatorsFromAssembly                 │  │
│  │ AddHttpContextAccessor                    │  │
│  └───────────────────────────────────────────┘  │
│                     calls ↓                     │
│  Layer 2 — Infrastructure (Infrastructure/Ext.) │
│  ┌───────────────────────────────────────────┐  │
│  │ AddQltSDBContext<T>       (database)      │  │
│  │ AddServiceBusEventBus     (event bus)     │  │
│  │ AddIdempotencyEntityFramework<T>          │  │
│  │ AddRedisIdempotencyCache                  │  │
│  │ AddTransactionalOutbox<T> (outbox)        │  │
│  │ Repositories                              │  │
│  └───────────────────────────────────────────┘  │
└─────────────────────────────────────────────────┘
```

| Layer | File | Registers |
|-------|------|-----------|
| **Application** | `Api/Extensions/Extensions.cs` | MediatR pipeline, validators, HTTP context |
| **Infrastructure** | `Infrastructure/Extensions/Extensions.cs` | DbContext, EventBus, idempotency, outbox, repositories |

---

## AddQltMediator

`AddQltMediator` is the single entry point for the MediatR pipeline. It replaces manual `AddMediatR` + behavior registration + telemetry calls.

### What it registers automatically

| Concern | Before (manual) | After (implicit) |
|---------|-----------------|-------------------|
| Pipeline behavior order | Developer must know and maintain | Platform-managed, always correct |
| `IdempotencyBehavior` | `cfg.AddOpenBehavior(typeof(...))` | Automatic |
| `IntegrationEventProcessorBehavior` | `cfg.AddOpenBehavior(typeof(...))` | Automatic |
| `TransactionBehavior` | `cfg.AddOpenBehavior(typeof(...))` | Automatic |
| `DomainEventProcessorBehavior` | `cfg.AddOpenBehavior(typeof(...))` | Automatic |
| `AddIntegrationEventTelemetryDecorator` | Manual call | Automatic |
| `AddDomainEventTelemetryBehavior` | Manual call | Automatic |
| `AddCommandTelemetryBehavior` | Manual call | Automatic |
| `AddQueryTelemetryBehavior` | Manual call | Automatic |

### What the developer configures

| Method | Required | Purpose |
|--------|----------|---------|
| `RegisterServicesFromAssembly(assembly)` | ✅ Yes | MediatR handler discovery |
| `WithLogging(typeof(LoggingBehavior<,>))` | Optional | Logging behavior at the outermost position |
| `WithValidation(typeof(ValidationBehavior<,>))` | Optional | Validation inside the transaction boundary |

### Pipeline order (managed by platform)

```
Logging (optional — WithLogging)
  └─ Idempotency (automatic)
       └─ IntegrationEventProcessor (automatic)
            └─ Transaction (automatic)
                 └─ Validation (optional — WithValidation)
                      └─ DomainEventProcessor (automatic)
                           └─ CommandHandler
```

All platform behaviors degrade gracefully when their dependencies are not registered:
- `IdempotencyBehavior` → passes through if `IIdempotencyService` is not registered
- `IntegrationEventProcessorBehavior` → passes through if `IEventBusOutboxService` is not registered
- `TransactionBehavior` → only activates for `ICommandRequest`
- `DomainEventProcessorBehavior` → passes through if `IDomainEntityChangeTracker` is not registered

---

## Complete Example: Accounts Service

### Layer 1 — Application (`Api/Extensions/Extensions.cs`)

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

### Layer 2 — Infrastructure (`Infrastructure/Extensions/Extensions.cs`)

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

## Minimal Example: Service Without Validation or Redis

A service that only consumes integration events (no API commands, no Redis):

### Application Layer

```csharp
public static void AddApplicationServices(this IHostApplicationBuilder builder)
{
    var services = builder.Services;

    builder.AddInfrastructureServices();

    services.AddHttpContextAccessor();

    services.AddQltMediator(cfg =>
    {
        cfg.RegisterServicesFromAssembly(typeof(AssemblyReference).Assembly);
        cfg.WithLogging(typeof(LoggingBehavior<,>));
    });

    services.AddScoped<IEmailService, EmailService>();
}
```

### Infrastructure Layer

```csharp
private static void AddInfrastructureServices(this IHostApplicationBuilder builder)
{
    var services = builder.Services;

    services.AddQltSDBContext<NotificationsContext>(
        conf => conf
            .SetDefaultSchema("notifications")
            .RegisterModelsFromAssembly(typeof(AssemblyReference).Assembly),
        options => options.UseSqlServer(connectionString, sqlOptions =>
        {
            sqlOptions.MigrationsAssembly(typeof(AssemblyReference).Assembly);
        })
    );

    services.AddMigration<NotificationsContext, NotificationsContextSeed>();

    builder.AddServiceBusEventBus("eventbus")
        .AddEventBusSubscriptions();

    services.AddIdempotencyEntityFramework<NotificationsContext>()
        .ForIntegrationEventHandlers();

    services.AddTransactionalOutbox<NotificationsContext>(options =>
    {
        options.RegisterEventTypes(typeof(Contracts.AssemblyReference).Assembly);
    });
}
```

---

## Infrastructure Component Reference

### Database

```csharp
services.AddQltSDBContext<MyContext>(
    conf => conf
        .SetDefaultSchema("myschema")
        .WithIdempotency()           // enables idempotency tables
        .WithAuditLogs()             // enables audit log tables
        .RegisterModelsFromAssembly(typeof(AssemblyReference).Assembly),
    options => options.UseSqlServer(connectionString, sqlOptions =>
    {
        sqlOptions.MigrationsAssembly(typeof(AssemblyReference).Assembly.FullName);
    })
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

### Idempotency — Redis + Entity Framework

```csharp
services.AddRedisIdempotencyCache(builder.Configuration)
    .ForApiRequests()
    .ForIntegrationEventHandlers();

services.AddIdempotencyEntityFramework<MyContext>()
    .ForApiRequests()
    .ForIntegrationEventHandlers();
```

| Method | Effect |
|--------|--------|
| `.ForApiRequests()` | Registers `IdempotencyBehavior` support for MediatR commands |
| `.ForIntegrationEventHandlers()` | Decorates `IIntegrationEventHandler<>` with idempotent consumer |

When both Redis and EF are registered, EF decorates Redis — Redis acts as fast cache, EF as transactional guarantee.

### Idempotency — Entity Framework Only

```csharp
services.AddIdempotencyEntityFramework<MyContext>()
    .ForIntegrationEventHandlers();
```

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
    .AddEventBusSubscriptions();

// In AddEventBusSubscriptions:
private static void AddEventBusSubscriptions(this IEventBusBuilder eventBus)
{
    eventBus.AddSubscription<SomeIntegrationEvent, SomeIntegrationEventHandler>();
}
```

---

## New Service Checklist

When creating a new service:

- [ ] Create `Api/Extensions/Extensions.cs` with `AddApplicationServices`
- [ ] Create `Infrastructure/Extensions/Extensions.cs` with `AddInfrastructureServices`
- [ ] Call `builder.AddInfrastructureServices()` from the application layer
- [ ] Use `AddQltMediator` — do NOT call `AddMediatR` directly
- [ ] Register handlers from all relevant assemblies via `RegisterServicesFromAssembly`
- [ ] Add `WithLogging(typeof(LoggingBehavior<,>))` if the service uses logging behavior
- [ ] Add `WithValidation(typeof(ValidationBehavior<,>))` if the service uses FluentValidation
- [ ] Configure `AddQltSDBContext<T>` with `.WithIdempotency()` if using idempotency
- [ ] Configure `AddTransactionalOutbox<T>` with the contracts assembly
- [ ] Do NOT call `AddIntegrationEventTelemetryDecorator`, `AddDomainEventTelemetryBehavior`, `AddCommandTelemetryBehavior`, or `AddQueryTelemetryBehavior` — these are implicit

---

## Common Mistakes

### ❌ Calling AddMediatR directly

```csharp
// ❌ Bypasses platform pipeline and telemetry
services.AddMediatR(cfg =>
{
    cfg.RegisterServicesFromAssembly(assembly);
    cfg.AddOpenBehavior(typeof(LoggingBehavior<,>));
    cfg.AddOpenBehavior(typeof(IdempotencyBehavior<,>));
    // ... developer must know all behaviors and their order
});
```

```csharp
// ✅ Platform manages pipeline order and telemetry
services.AddQltMediator(cfg =>
{
    cfg.RegisterServicesFromAssembly(assembly);
    cfg.WithLogging(typeof(LoggingBehavior<,>));
});
```

### ❌ Manually registering telemetry

```csharp
// ❌ No longer needed — implicit in AddQltMediator
services.AddIntegrationEventTelemetryDecorator();
services.AddDomainEventTelemetryBehavior();
services.AddCommandTelemetryBehavior();
services.AddQueryTelemetryBehavior();
```

### ❌ Mixing application and infrastructure concerns

```csharp
// ❌ Infrastructure and application mixed in one method
public static void AddApplicationServices(this IHostApplicationBuilder builder)
{
    services.AddQltSDBContext<MyContext>(...);    // infrastructure
    services.AddQltMediator(cfg => { ... });     // application
    services.AddTransactionalOutbox<T>(...);     // infrastructure
}
```

```csharp
// ✅ Clear separation
public static void AddApplicationServices(this IHostApplicationBuilder builder)
{
    builder.AddInfrastructureServices();          // delegates infrastructure

    services.AddQltMediator(cfg => { ... });      // application only
    services.AddValidatorsFromAssembly(...);      // application only
}

private static void AddInfrastructureServices(this IHostApplicationBuilder builder)
{
    services.AddQltSDBContext<MyContext>(...);     // infrastructure
    services.AddTransactionalOutbox<T>(...);      // infrastructure
}
```
