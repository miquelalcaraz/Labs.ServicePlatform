# Developer Guide: Transactional Pipeline and Domain Events

## Overview

Practical guide for developers working with the transactional pipeline. The core design follows the **eShop reference architecture**: strong consistency within a bounded context (one transaction), eventual consistency between bounded contexts (integration events via outbox).

---

## Pipeline Execution Order

Register via `AddQltMediator` — the platform manages all behavior ordering:

```csharp
services.AddQltMediator(cfg =>
{
    cfg.RegisterServicesFromAssembly(assembly);
    cfg.WithLogging(typeof(LoggingBehavior<,>));
    cfg.WithValidation(typeof(ValidationBehavior<,>));
});
```

Resulting execution (Russian dolls):

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
                      │   └─ Handlers may add OutboxMessages
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

When `TransactionBehavior` has started a transaction, `SaveChangesAsync()` **flushes pending changes to the database but does NOT commit**. Changes are visible within the same transaction but invisible to other connections and will be rolled back if the transaction fails.

```csharp
public async Task<Result<bool>> Handle(CreateOrganizationCommand message, CancellationToken ct)
{
    var organization = Organization.Create(message.Name, ...);
    _organizationRepository.Add(organization);

    // FLUSH to DB (generates IDs, applies defaults) but does NOT commit.
    var saved = await _organizationRepository.UnitOfWork.SaveChangesAsync(ct);

    return saved > 0
        ? Result.Success(true)
        : Result.Failure<bool>(Error.BadRequest.WithMessage("Failed to create organization"));
}
```

### Why the flush is necessary

After the handler's `SaveChangesAsync()`, domain event handlers can:
- Query the database for the newly created entity (it exists within the transaction)
- Access database-generated values (identity columns, defaults)
- Add OutboxMessages that reference the entity's ID

### The two SaveChangesAsync calls

| Call | When | What it saves |
|------|------|---------------|
| **1st** — in CommandHandler | During handler execution | Aggregate entity, idempotency entry |
| **2nd** — in CommitTransactionAsync | At commit time | OutboxMessages, handler modifications, audit logs |

Both execute within the **same database transaction**. Neither is a commit on its own.

---

## Domain Event Handler Rules

### DO

- ✅ Keep handlers lightweight (under ~100ms)
- ✅ Add integration events via `IEventBusOutboxService.AddAndSaveEventAsync()`
- ✅ Query entities flushed by the command handler's `SaveChangesAsync()`
- ✅ Modify other aggregates (changes committed atomically)
- ✅ Raise new domain events on modified aggregates (cascade loop handles them)

### DO NOT

- ❌ Call `SaveChangesAsync()` in a domain event handler — `CommitTransactionAsync` handles it
- ❌ Start new transactions
- ❌ Perform heavy computation or external HTTP calls (extends transaction lock time)
- ❌ Assume the entity is "permanently saved" — it's only flushed, not committed
- ❌ Swallow exceptions silently — failure must propagate to trigger rollback

### Example

```csharp
public class OrganizationCreatedDomainEventHandler : IDomainEventHandler<OrganizationCreatedDomainEvent>
{
    private readonly IOrganizationRepository _repository;
    private readonly IEventBusOutboxService _outboxService;

    public async Task Handle(OrganizationCreatedDomainEvent notification, CancellationToken ct)
    {
        var organization = await _repository.GetAsync(notification.OrganizationId);
        if (organization is null) return;

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

## When to Use Domain Events vs Integration Events

| Scenario | Use |
|----------|-----|
| Side effect within same bounded context that must be atomic | **Domain Event** |
| Notify other bounded contexts about a state change | **Integration Event** (via Outbox) |
| Heavy processing (email, PDF, external API) | **Integration Event** → async handler |
| Modify another aggregate in the same context | **Domain Event** (within transaction) |
| Fire-and-forget notification | **Integration Event** |

---

## Risk Mitigation

### Transaction Duration

Long-running domain event handlers extend transaction duration. Keep handlers under ~100ms. If processing is heavy, publish an integration event and handle it asynchronously.

### Cascading Events

`DomainEventProcessorBehavior` handles cascades via a `while` loop — safe but monitor depth. Design domain events as leaf operations when possible.

### Handler Failure = Full Rollback

If ANY domain event handler throws, the ENTIRE transaction rolls back. This is by design (atomic consistency). If the aggregate must survive handler failures, use an integration event instead.

### AddAndSaveEventAsync Requires Active Transaction

`IEventBusOutboxService.AddAndSaveEventAsync()` throws `InvalidOperationException` without an active transaction. Only call from within the pipeline.

---

## Failure Scenarios

| Scenario | Result |
|----------|--------|
| Command handler throws | Transaction ROLLBACK. Nothing persisted. Client receives error. |
| Domain event handler throws | Transaction ROLLBACK. Everything reverted (aggregate + outbox + idempotency). |
| CommitTransactionAsync fails | Transaction ROLLBACK. ExecutionStrategy retries if transient. |
| Integration event publish fails (after commit) | Aggregate IS committed. OutboxMessage status = Failed. OutboxProcessor retries. |

---

## Quick Reference: What Runs Where

| Component | Inside Transaction | After Commit |
|-----------|-------------------|--------------|
| CommandHandler | ✅ | |
| DomainEventProcessorBehavior | ✅ | |
| Domain event handlers | ✅ | |
| ValidationBehavior | ✅ | |
| CommitTransactionAsync | ✅ (final flush + commit) | |
| IntegrationEventProcessorBehavior | | ✅ |
| OutboxProcessor (background) | | ✅ |
| IdempotencyBehavior.MarkAsCompleted (cache) | | ✅ |
