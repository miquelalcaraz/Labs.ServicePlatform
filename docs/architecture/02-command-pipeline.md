# Command Pipeline

Covers the Result Pattern, CQRS, Pipeline Behaviors, and Unit of Work — the core request processing infrastructure.

---

## Result Pattern

All commands return `Result` or `Result<T>` (Railway-Oriented Programming). Located in `Platform.Core`.

```csharp
// Success track
return Result.Success(true);

// Failure track
return Result.Failure<bool>(Error.BadRequest.WithMessage("Organization name is required"));
```

**Key types:** `Result`, `Result<T>`, `Error` (immutable record with `Code`, `Message`, `PropertyName`).

Pre-defined errors: `Error.None`, `Error.NotFound`, `Error.Conflict`, `Error.BadRequest`, `Error.Unauthorized`.

---

## CQRS with MediatR

Commands mutate state and return `Result<T>`. Queries retrieve data without side effects. Both flow through MediatR's pipeline.

### Command Abstractions

```csharp
// Marker for pipeline filtering (zero-reflection pattern matching)
public interface ICommandRequest { }

public interface ICommand<TResponse> : IRequest<Result<TResponse>>, ICommandRequest { }

public interface ICommandHandler<TCommand, TResponse> : IRequestHandler<TCommand, Result<TResponse>>
    where TCommand : ICommand<TResponse> { }
```

### Query Abstractions

```csharp
public interface IQuery<TResponse> : IRequest<Result<TResponse>> { }

public interface IQueryHandler<TQuery, TResponse> : IRequestHandler<TQuery, Result<TResponse>>
    where TQuery : IQuery<TResponse> { }
```

### Domain Event Abstractions

```csharp
// Domain.Core (pure DDD, no dependencies)
public interface IDomainEvent { }

// Platform.Messaging.Abstractions (MediatR adapter)
public interface IDomainEventHandler<TEvent> : INotificationHandler<TEvent>
    where TEvent : IDomainEvent, INotification { }
```

### Read/Write DbContext Separation

```csharp
// Write: transactions, audit, idempotency, outbox
public abstract class QltDBContext : DbContext, ITransactionalUnitOfWork, IDomainEntityChangeTracker { }

// Read: NoTracking, throws on write attempts, read-replica ready
public abstract class QltReadDbContext : DbContext
{
    // SaveChanges() sealed to throw InvalidOperationException
}
```

Query handlers inject the read context; command handlers use the write context via repositories.

---

## Pipeline Behaviors

Behaviors wrap command/query execution using MediatR's Chain of Responsibility pattern. Each behavior has a single responsibility.

### Registration Order

```csharp
services.AddQltMediator(cfg =>
{
    cfg.RegisterServicesFromAssembly(assembly);
    cfg.AddOpenBehavior(typeof(LoggingBehavior<,>));                    // 1. Outermost
    cfg.AddOpenBehavior(typeof(IdempotencyBehavior<,>));                // 2. Duplicate detection
    cfg.AddOpenBehavior(typeof(IntegrationEventProcessorBehavior<,>)); // 3. Outbox publishing (AFTER commit)
    cfg.AddOpenBehavior(typeof(TransactionBehavior<,>));                // 4. Transaction management
    cfg.AddOpenBehavior(typeof(ValidationBehavior<,>));                 // 5. FluentValidation
    cfg.AddOpenBehavior(typeof(DomainEventProcessorBehavior<,>));      // 6. Domain events (WITHIN transaction)
});

// Telemetry registered separately (as IPipelineBehavior, not open behaviors)
services.AddCommandTelemetryBehavior();
services.AddQueryTelemetryBehavior();
```

### Pipeline Flow

```
Request
  ↓
TelemetryBehavior (OpenTelemetry activity + metrics)
  ↓
LoggingBehavior (timer, request/correlation IDs, slow detection >500ms)
  ↓
IdempotencyBehavior (check duplicate → skip if exists, mark as processing in change tracker)
  ↓
IntegrationEventProcessorBehavior (pass through → publish outbox on return)
  ↓
TransactionBehavior (begin transaction)
  ↓
ValidationBehavior (FluentValidation)
  ↓
DomainEventProcessorBehavior (pass through → dispatch domain events on return)
  ↓
CommandHandler (business logic, SaveChangesAsync)
  ↓
DomainEventProcessorBehavior (dispatch domain events, cascading re-check loop)
  ↓
TransactionBehavior (CommitTransactionAsync, set LastCommittedTransactionId)
  ↓
IntegrationEventProcessorBehavior (read transactionId, publish outbox to event bus)
  ↓
IdempotencyBehavior (mark as completed in cache)
  ↓
LoggingBehavior (log end, detect slow commands)
  ↓
Response
```

### How Behaviors Filter Commands vs Queries

| Behavior | Filter | Queries |
|---|---|---|
| TransactionBehavior | `request is not ICommandRequest` → skip | Skipped |
| IdempotencyBehavior | `request is not IIdempotentRequest` → skip | Skipped |
| DomainEventProcessorBehavior | No active transaction → early return | No-op |
| IntegrationEventProcessorBehavior | No `LastCommittedTransactionId` → skip | Skipped |
| TelemetryCommandBehavior | Checks `ICommand` | Skipped |
| TelemetryQueryBehavior | Checks `IQuery<>` | Active |
| LoggingBehavior / ValidationBehavior | No filter | Active |

### Behavior Locations

| Behavior | Project | Why |
|---|---|---|
| LoggingBehavior, ValidationBehavior | Domain.Application | Cross-cutting, no infrastructure dependency |
| IdempotencyBehavior | Platform.Messaging | Uses `IIdempotencyService` abstraction, no EF |
| TelemetryCommandBehavior, TelemetryQueryBehavior | Platform.Messaging | OpenTelemetry, no EF |
| TransactionBehavior | Platform.Messaging.EntityFramework | Needs both `ICommandRequest` + `ITransactionalUnitOfWork` |
| DomainEventProcessorBehavior | Platform.Messaging.EntityFramework | Needs `IDomainEntityChangeTracker` + `IMediator` |
| IntegrationEventProcessorBehavior | Platform.Messaging.EntityFramework | Needs `ITransactionalUnitOfWork` + `IEventBusOutboxService` |

---

## Unit of Work

EF Core's `DbContext` serves as the Unit of Work. The interface is layered:

```csharp
// Domain.Core — minimal contract for repositories
public interface IUnitOfWork : IDisposable
{
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}

// Platform.Persistence.EntityFramework — full transaction support
public interface ITransactionalUnitOfWork : IUnitOfWork
{
    Task<IDbContextTransaction?> BeginTransactionAsync();
    Task CommitTransactionAsync(IDbContextTransaction transaction, CancellationToken cancellationToken = default);
    void RollbackTransaction();
    bool HasActiveTransaction { get; }
    Guid? LastCommittedTransactionId { get; }
    IExecutionStrategy CreateExecutionStrategy();
}
```

`QltDBContext` also implements `IDomainEntityChangeTracker` for domain event dispatch:

```csharp
public interface IDomainEntityChangeTracker
{
    IReadOnlyList<Entity> GetEntitiesWithChanges();
}
```

### Transaction Lifecycle

1. `TransactionBehavior` calls `BeginTransactionAsync()`
2. Inner pipeline runs: validation → command handler → `SaveChangesAsync()`
3. `DomainEventProcessorBehavior` dispatches domain events (cascading re-check loop)
4. `TransactionBehavior` calls `CommitTransactionAsync()` → sets `LastCommittedTransactionId`
5. `IntegrationEventProcessorBehavior` reads the transaction ID and publishes outbox events to the event bus

---

## Design Decisions

**Why `ICommandRequest` marker?** C# lacks open generic pattern matching. The marker enables `request is not ICommandRequest` for zero-reflection filtering.

**Why separate telemetry behaviors for commands and queries?** Distinct OpenTelemetry counters (`command_handlers.processed` vs `query_handlers.processed`) and histograms for independent read vs write monitoring.

**Why query handlers in Infrastructure?** They depend on the concrete read `DbContext`, an infrastructure concern. Query records (`IQuery<T>`) live in Application as pure contracts.

**Why `TransactionBehavior` in `Platform.Messaging.EntityFramework`?** It needs both `ICommandRequest` (messaging concept) and `ITransactionalUnitOfWork` (persistence concept). This layer has access to both.
