# Messaging & Persistence Layers

## Overview

The messaging and persistence infrastructure is organized in a **5-layer architecture** where each layer has a single responsibility and strict dependency rules. Higher layers depend only on lower layers, and database-specific knowledge is isolated to the outermost layer.

```
Platform.Messaging.Abstractions    (Pure contracts)
    ↓
Platform.Messaging                 (Logic, POCOs, behaviors — no EF)
    ↓
Platform.Persistence.EntityFramework         (EF abstractions — no DB specifics)
    ↓
    ├── Platform.Messaging.EntityFramework   (Messaging + EF — DB-agnostic)
    └── Platform.Persistence.EntityFramework.Relational (SQL/PostgreSQL specifics)
```

---

## Layer-by-Layer

### 1. Platform.Messaging.Abstractions

Pure contracts without infrastructure dependencies.

**Owns:**
- `IIntegrationEventHandler<T>`, `ICommandHandler<T,R>`, `IDomainEventHandler<T>`
- `IIdempotencyService` (unified: `ExistsAsync`, `MarkAsProcessingAsync`, `MarkAsCompletedAsync`)
- `IEventBusOutboxService` (`AddAndSaveEventAsync`, `PublishEventsThroughEventBusAsync`)
- Base types: `IntegrationEvent`, `ICommand<T>`, `IQuery<T>`

**Dependencies:** MediatR, Microsoft.Extensions.Options, Domain.Core, Platform.Core

**Must not reference:** Entity Framework, database providers, implementation logic.

---

### 2. Platform.Messaging

Messaging logic and domain models without any persistence dependency.

**Owns:**
- **POCOs** (persistence-ignorant): `IdempotencyEntry`, `OutboxMessage`, `OutboxMessageStatus`
- **Behaviors**: `IdempotencyBehavior<T,R>`, `TelemetryCommandBehavior`, `TelemetryQueryBehavior`
- **Domain event telemetry**: `TelemetryDomainEventBehavior` (custom `INotificationPublisher`)
- **Domain event dispatch**: `DomainEventsDispatcherExtension` (IMediator extension method)
- **Decorators**: `TelemetryIntegrationEventHandlerDecorator<T>`, `IdempotencyIntegrationEventDecorator<T>`
- **Command abstractions**: `IdempotentCommand<T>`
- DI registration extensions

**Dependencies:** Platform.Messaging.Abstractions, Platform.Telemetry, MediatR, Scrutor

**Must not reference:** Entity Framework, database providers. POCOs have no EF attributes.

---

### 3. Platform.Persistence.EntityFramework

EF Core abstractions without database-specific knowledge.

**Owns:**
- `ITransactionalUnitOfWork` (extends `IUnitOfWork` with transaction capabilities + `LastCommittedTransactionId`)
- `IDomainEntityChangeTracker` (exposes tracked entities for domain event dispatch)
- `QltDbContextConfiguration`, `QltReadDbContextConfiguration` (fluent API for feature toggles)
- `UniqueConstraintViolationException`

**Dependencies:** Microsoft.EntityFrameworkCore, MediatR, Domain.Core, Platform.Core, Platform.Telemetry

**Must not reference:** Database providers (SqlClient, Npgsql), entity configurations, messaging.

---

### 4. Platform.Messaging.EntityFramework

Messaging services that use EF Core through generic constraints — works with any `DbContext`.

**Owns:**
- **Services**: `IdempotencyEfService<TContext>`, `OutboxMessageService<TContext>`, `EventBusOutboxService<TContext>`
- **Decorators**: `IdempotencyIntegrationEventEfDecorator<T>`
- **Pipeline behaviors**: `TransactionBehavior<T,R>`, `DomainEventProcessorBehavior<T,R>`, `IntegrationEventProcessorBehavior<T,R>`
- **Background services**: `OutboxProcessor` (hosted service for pending outbox events)

**Dependencies:** Platform.Messaging, Platform.Persistence.EntityFramework, Platform.EventBus, Platform.Telemetry, Microsoft.EntityFrameworkCore

**Must not reference:** SQL Server, PostgreSQL, Cosmos, entity configurations, table names.

All services use `TContext where TContext : DbContext` — never a concrete context.

---

### 5. Platform.Persistence.EntityFramework.Relational

SQL Server and PostgreSQL specifics. This is the **only** layer that knows about relational databases.

**Owns:**
- `QltDBContext` (implements `ITransactionalUnitOfWork` + `IDomainEntityChangeTracker`)
- **Entity configurations** (all `internal`): `IdempotencyEntryConfiguration`, `OutboxMessageConfiguration`, `AuditLogConfiguration`
- `ResilientTransaction` (retry logic)
- `Repository<T>` (generic repository)
- `MigrateDbContextExtensions`
- `AddQltSDBContext<TContext>()` registration

**Dependencies:** Platform.Persistence.EntityFramework, Platform.Messaging.EntityFramework, Platform.Messaging, Microsoft.EntityFrameworkCore.Relational, Microsoft.Data.SqlClient, Npgsql

---

## Dependency Flow

```
Platform.Messaging.Abstractions ← Pure contracts
        ▲
        │
Platform.Messaging ← POCOs, behaviors, decorators (no EF)
        ▲
        │
Platform.Persistence.EntityFramework ← EF abstractions (no DB)
        ▲
        ├──────────────────────────────────┐
        │                                  │
Platform.Messaging.EntityFramework    Platform.Persistence.EF.Relational
(EF services, generic TContext)       (SQL/PostgreSQL, configurations)
```

---

## Key Design Principles

### Persistence Ignorance

POCOs in `Platform.Messaging` have no EF attributes:

```csharp
// Platform.Messaging/Models/IdempotencyEntry.cs
public class IdempotencyEntry
{
    public Guid Id { get; set; }
    public IdempotencyScope Scope { get; set; }
    public string Qualifier { get; set; } = string.Empty;
    public string? TypeName { get; set; }
    public DateTime ProcessedAt { get; set; }
}
```

Mapping is handled in `Platform.Persistence.EntityFramework.Relational`:

```csharp
// Configurations/IdempotencyEntryConfiguration.cs (internal)
internal static class IdempotencyEntryConfiguration
{
    public static void Configure(this EntityTypeBuilder<IdempotencyEntry> entity)
    {
        entity.ToTable("IdempotencyEntry", "infra");
        entity.HasKey(e => new { e.Id, e.Scope, e.Qualifier });
    }
}
```

### Generic Constraints Over Concrete Types

```csharp
// ✅ Any DbContext works
public class IdempotencyEfService<TContext> where TContext : DbContext
{
    private readonly TContext _context;
}
```

### Fluent DbContext Configuration

```csharp
services.AddQltSDBContext<AccountsContext>(
    conf => conf
        .WithIdempotency()
        .WithTransactionalOutbox()
        .SetDefaultSchema("accounts"),
    options => options.UseSqlServer(connectionString)
);
```

---

## Extensibility

### Adding Cosmos DB

Create `Platform.Persistence.EntityFramework.Cosmos` with Cosmos-specific entity configurations. **No changes** to `Platform.Messaging` (POCOs reused), `Platform.Messaging.EntityFramework` (generic services reused), or existing services.

### Adding Dapper

Create `Platform.Messaging.Dapper` implementing `IIdempotencyService` and `IEventBusOutboxService`. **No changes** to abstractions or existing handlers.

### Adding Redis Idempotency

Already implemented in `Platform.Messaging.Redis`. Implements `IIdempotencyService` and is optionally wrapped by `IdempotencyEfService<TContext>` as a fast cache layer.

---

## Validation Rules

Before modifying these layers:

- `Platform.Messaging` must NOT reference `EntityFrameworkCore`
- `Platform.Messaging.EntityFramework` must NOT reference SQL/PostgreSQL providers
- POCOs in `Platform.Messaging` must have NO EF attributes
- Entity configurations must live ONLY in `Platform.Persistence.EntityFramework.Relational`
- Services in `Platform.Messaging.EntityFramework` must use generic `TContext where TContext : DbContext`
