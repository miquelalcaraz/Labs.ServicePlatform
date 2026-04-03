# Developer Guide: Transactional Pipeline and Domain Events

## Overview

This document is a **practical guide for developers** working with the transactional pipeline in QltS services. It explains how the single-transaction model works, what happens under the hood, and the precautions you must take to avoid common pitfalls.

### Architectural Context

This architecture follows the **eShop reference architecture** (Microsoft) and principles from *Patterns of Enterprise Application Architecture* (Martin Fowler). The core decision is:

- **Strong consistency within a bounded context** — one transaction wraps the command handler, all domain event handlers, and cascading events
- **Eventual consistency between bounded contexts** — integration events are published via the Transactional Outbox after commit

This is NOT a flaw to be corrected. It is an intentional trade-off that eliminates compensation/saga infrastructure for local domain events.

---

## Pipeline Execution Order

> **Note:** Use `AddQltMediator` instead of manual `AddMediatR` — it registers all platform behaviors in the correct order and includes telemetry automatically. See [20-developer-guide-service-configuration.md](20-developer-guide-service-configuration.md).

```csharp
services.AddQltMediator(cfg =>
{
    cfg.RegisterServicesFromAssembly(assembly);
    cfg.WithLogging(typeof(LoggingBehavior<,>));      // 1 — outermost
    cfg.WithValidation(typeof(ValidationBehavior<,>)); // 5 — inside transaction
});
```

The platform registers the remaining behaviors automatically in this order:

```
1. LoggingBehavior          (WithLogging — optional, from Domain layer)
2. IdempotencyBehavior      (automatic)
3. IntegrationEventProcessorBehavior (automatic — OUTSIDE transaction)
4. TransactionBehavior      (automatic — transaction boundary)
5. ValidationBehavior       (WithValidation — optional, INSIDE transaction)
6. DomainEventProcessorBehavior (automatic — INSIDE transaction)
+ Telemetry (Command, Query, DomainEvent, IntegrationEvent — always implicit)
```

The resulting execution (Russian dolls):

```
Logging
  └─ Idempotency (check duplicate)
       └─ IntegrationEventProcessor (pass-through on the way in)
            └─ TransactionBehavior: BEGIN TRANSACTION
                 └─ Validation
                      └─ DomainEventProcessor (pass-through on the way in)
                           └─ CommandHandler
                              ├── Creates/modifies aggregate
                              └── Calls SaveChangesAsync() — FLUSH, not commit
                      ┌─ DomainEventProcessor: dispatches domain events
                      │   └─ Handlers may add OutboxMessages to change tracker
                      │   └─ Handlers may modify other aggregates
                      │   └─ Cascade loop: checks for new events
                 └─ Validation (return)
            └─ TransactionBehavior: CommitTransactionAsync()
            │   ├── SaveChangesAsync() — flushes OutboxMessages + handler changes
            │   └── CommitAsync() — ATOMIC COMMIT of everything
       └─ IntegrationEventProcessor: publishes OutboxMessages to event bus
  └─ Idempotency (mark completed in cache)
Logging (log completion)
```

---

## SaveChangesAsync Does NOT Commit

This is the most important concept to understand.

### What `SaveChangesAsync()` does inside a transaction

When a `TransactionBehavior` has started a transaction, calling `SaveChangesAsync()` **flushes pending changes to the database but does NOT commit them**. The changes are visible within the same transaction (same `DbContext`) but are invisible to other connections and will be rolled back if the transaction fails.

```csharp
public async Task<Result<bool>> Handle(CreateOrganizationCommand message, CancellationToken ct)
{
    var organization = Organization.Create(message.Name, ...);
    _organizationRepository.Add(organization);

    // This FLUSHES to DB (generates IDs, applies defaults) but does NOT commit.
    // If anything fails later, this will be rolled back.
    var saved = await _organizationRepository.UnitOfWork.SaveChangesAsync(ct);

    return saved > 0
        ? Result.Success(true)
        : Result.Failure<bool>(Error.BadRequest.WithMessage("Failed to create organization"));
}
```

### Why the flush is necessary

After the handler's `SaveChangesAsync()`, domain event handlers can:
- **Query the database** for the newly created entity (it exists within the transaction)
- **Access database-generated values** (identity columns, defaults, computed columns)
- **Add OutboxMessages** that reference the entity's ID

### What actually commits

`CommitTransactionAsync()` in `QltDBContext` performs the real commit:

```
CommitTransactionAsync()
  ├── base.SaveChangesAsync()  → flushes OutboxMessages + handler modifications
  ├── SaveAuditLogsAsync()     → if audit logs enabled
  └── transaction.CommitAsync() → ATOMIC COMMIT — everything or nothing
```

### The two SaveChangesAsync calls

| Call | When | What it saves |
|------|------|---------------|
| **1st** — in CommandHandler | During handler execution | Aggregate entity, idempotency entry |
| **2nd** — in CommitTransactionAsync | At commit time | OutboxMessages, handler modifications, audit logs |

Both execute within the **same database transaction**. Neither is a commit on its own.

---

## Domain Event Handler Rules

### DO

- ✅ Keep handlers lightweight (minimal I/O)
- ✅ Add integration events to the outbox via `IEventBusOutboxService.AddAndSaveEventAsync()`
- ✅ Query entities that were flushed by the command handler's `SaveChangesAsync()`
- ✅ Modify other aggregates if needed (changes are tracked and committed atomically)
- ✅ Raise new domain events on modified aggregates (the cascade loop handles them)

### DO NOT

- ❌ Call `SaveChangesAsync()` in a domain event handler — let `CommitTransactionAsync` handle it
- ❌ Start new transactions — the handler runs within the existing transaction
- ❌ Perform heavy computation or external HTTP calls — this extends the transaction duration and lock time
- ❌ Assume the entity is "permanently saved" — it's only flushed, not committed
- ❌ Swallow exceptions silently — a failure in a handler must propagate to trigger rollback

### Example: Correct Domain Event Handler

```csharp
public class OrganizationCreatedDomainEventHandler : IDomainEventHandler<OrganizationCreatedDomainEvent>
{
    private readonly IOrganizationRepository _repository;
    private readonly IEventBusOutboxService _outboxService;

    public async Task Handle(OrganizationCreatedDomainEvent notification, CancellationToken ct)
    {
        // Query works because CommandHandler already called SaveChangesAsync()
        var organization = await _repository.GetAsync(notification.OrganizationId);

        if (organization is null) return;

        // Adds OutboxMessage to change tracker (no SaveChangesAsync needed)
        await _outboxService.AddAndSaveEventAsync(
            new OrganizationCreatedIntegrationEvent(
                organization.TenantId,
                organization.Id,
                organization.Name,
                organization.LegalName,
                organization.TaxNumber,
                organization.Address?.CountryCode!));
    }
}
```

---

## Risk Mitigation

### 1. Transaction Duration

**Risk**: Long-running domain event handlers extend transaction duration, increasing database lock time and deadlock probability.

**Mitigation**:
- Keep domain event handlers under **100ms**. If processing takes longer, publish an integration event instead and handle it asynchronously.
- Monitor transaction duration via OpenTelemetry (`TransactionBehavior` emits activity spans with duration).
- The `LoggingBehavior` detects commands exceeding 500ms and logs warnings.

### 2. Cascading Events

**Risk**: A domain event handler modifies an aggregate that raises another domain event, creating a chain. Deep chains extend the transaction.

**Mitigation**:
- `DomainEventProcessorBehavior` handles cascades via a `while` loop — this is safe but monitor depth.
- Design domain events to be **leaf operations** when possible (no cascading).
- If a cascade is necessary, ensure each handler is lightweight.

### 3. Handler Failure = Full Rollback

**Risk**: If ANY domain event handler throws an exception, the ENTIRE transaction rolls back — including the aggregate created by the command handler.

**Mitigation**:
- This is by design (atomic consistency). If you need the aggregate to survive handler failures, the handler's work should be an integration event (eventual consistency).
- Test domain event handlers thoroughly — they are part of the command's critical path.
- Use `try-catch` only if partial failure is acceptable AND you explicitly log the error.

### 4. AddAndSaveEventAsync Requires Active Transaction

**Risk**: `IEventBusOutboxService.AddAndSaveEventAsync()` throws `InvalidOperationException` if called without an active transaction.

**Mitigation**:
- Only call it from within the pipeline (domain event handlers, command handlers).
- Never call it from background services or HTTP middleware directly.

### 5. Re-querying Entities in Handlers

**Risk**: The command handler's `SaveChangesAsync()` flush makes entities queryable within the transaction, but the query hits the database (extra round-trip).

**Mitigation**:
- Pass entity data via the domain event record when possible:
  ```csharp
  // Avoid extra query
  public record OrganizationCreatedDomainEvent(
      Guid OrganizationId,
      string Name,
      string TaxNumber,
      Guid TenantId) : IDomainEvent, INotification;
  ```
- If you need navigation properties or complex data, the query is acceptable.

---

## When to Use Domain Events vs Integration Events

| Scenario | Use |
|----------|-----|
| Side effect **within same bounded context** that must be atomic | **Domain Event** |
| Notify **other bounded contexts** about a state change | **Integration Event** (via Outbox) |
| Heavy processing (email, PDF, external API) | **Integration Event** → async handler |
| Modify another aggregate in the same context | **Domain Event** (within transaction) |
| Fire-and-forget notification | **Integration Event** |

---

## Failure Scenarios

### Scenario 1: Command handler throws

```
Transaction BEGIN → CommandHandler throws → Transaction ROLLBACK
Result: Nothing persisted. Clean state. Client receives error.
```

### Scenario 2: Domain event handler throws

```
Transaction BEGIN → CommandHandler OK → SaveChangesAsync (flush) →
DomainEventHandler throws → Transaction ROLLBACK
Result: Everything reverted (aggregate + outbox + idempotency). Client receives error.
```

### Scenario 3: CommitTransactionAsync fails

```
Transaction BEGIN → CommandHandler OK → DomainEventHandlers OK →
CommitTransactionAsync throws → Transaction ROLLBACK
Result: Everything reverted. Client receives error.
ExecutionStrategy retries if transient failure.
```

### Scenario 4: Integration event publish fails (after commit)

```
Transaction BEGIN → ... → COMMIT OK →
IntegrationEventProcessor: PublishAsync fails
Result: Aggregate IS committed. OutboxMessage state = Failed.
OutboxProcessor (background) retries periodically.
```

---

## Quick Reference: What Runs Where

| Component | Inside Transaction | After Commit |
|-----------|-------------------|--------------|
| `CommandHandler` | ✅ | |
| `DomainEventProcessorBehavior` | ✅ | |
| Domain event handlers | ✅ | |
| `ValidationBehavior` | ✅ | |
| `CommitTransactionAsync` | ✅ (final flush + commit) | |
| `IntegrationEventProcessorBehavior` | | ✅ |
| `OutboxProcessor` (background) | | ✅ |
| `IdempotencyBehavior.MarkAsCompleted` (cache) | | ✅ |

---

## References

- [eShop Reference Architecture — Domain Events](https://github.com/dotnet-architecture/eShopOnContainers)
- [Patterns of Enterprise Application Architecture — Martin Fowler](https://martinfowler.com/eaaCatalog/)
- [doc 03 — Domain Events Pattern](03-domain-events-pattern.md)
- [doc 07 — Unit of Work Pattern](07-unit-of-work-pattern.md)
- [doc 08 — Pipeline Behavior Pattern](08-pipeline-behavior-pattern.md)
- [doc 11 — Two-SaveChanges Pattern](11-two-savechanges-pattern.md)
