# Developer Guide: End-to-End Flow — CreateOrganization

## Overview

Walkthrough of the `CreateOrganization` endpoint showing how **CQRS**, **Domain Events**, **Integration Events**, **Transactional Outbox**, **Idempotent Consumer**, **Unit of Work**, and **Result Pattern** work together in a real flow.

---

## Flow Diagram

```
Client → PUT /api/accounts [x-requestid: guid]
  │
  ├─ API validates requestId, creates CreateOrganizationCommand
  │
  ├─ MediatR Pipeline:
  │   1. LoggingBehavior (start timer)
  │   2. IdempotencyBehavior (check IIdempotencyService.ExistsAsync)
  │   3. IntegrationEventProcessorBehavior (pass-through in)
  │   4. TransactionBehavior (BEGIN TRANSACTION)
  │   5. ValidationBehavior (FluentValidation)
  │   6. DomainEventProcessorBehavior (pass-through in)
  │   7. CommandHandler:
  │       ├─ Organization.Create() → raises OrganizationCreatedDomainEvent
  │       ├─ Repository.Add(organization)
  │       └─ SaveChangesAsync() → FLUSH (not commit)
  │   6. DomainEventProcessorBehavior (dispatches domain events):
  │       └─ OrganizationCreatedDomainEventHandler:
  │           ├─ Queries organization (visible in transaction)
  │           └─ AddAndSaveEventAsync(OrganizationCreatedIntegrationEvent)
  │   4. TransactionBehavior:
  │       ├─ SaveChangesAsync() → flushes OutboxMessages
  │       └─ CommitAsync() → ATOMIC COMMIT
  │   3. IntegrationEventProcessorBehavior:
  │       └─ PublishEventsThroughEventBusAsync(transactionId)
  │   2. IdempotencyBehavior (mark completed in cache)
  │   1. LoggingBehavior (log completion)
  │
  ├─ API returns Result → HTTP 200 OK
  │
  ├─ Event Bus delivers OrganizationCreatedIntegrationEvent:
  │   ├─ Identity Service:
  │   │   ├─ TelemetryDecorator → IdempotencyDecorator → Handler
  │   │   └─ Creates Tenant + Admin User (within transaction)
  │   └─ Notifications Service:
  │       ├─ TelemetryDecorator → IdempotencyDecorator → Handler
  │       └─ Sends welcome email + sales notification
  │
  └─ Eventual Consistency achieved
```

---

## Phase-by-Phase

### 1. API Endpoint

```
PUT /api/accounts HTTP/1.1
x-requestid: {guid}
Content-Type: application/json
```

Validates `requestId` header, creates `CreateOrganizationCommand`, sends to MediatR. Returns `Ok(true)` or `BadRequest(error)` via Result Pattern.

### 2. IdempotencyBehavior

- Pattern-matches `IIdempotentCommand` (zero reflection)
- Checks `IdempotencyKey` via `IIdempotencyService.ExistsAsync(key)` — key: `(Scope: "CreateOrganizationCommand", Id: guid, Qualifier: null)`
- If exists: returns `CreateResultForDuplicateRequest()` without executing handler
- If new: calls `MarkAsProcessingAsync()` (adds to EF change tracker, no `SaveChanges` yet)

### 3. TransactionBehavior

Begins database transaction, executes inner pipeline, commits atomically:

```csharp
await using var transaction = await _context.BeginTransactionAsync();
response = await next(cancellationToken);
await _context.CommitTransactionAsync(transaction);
```

### 4. CommandHandler

```csharp
var organization = Organization.Create(message.Name, message.LegalName, address, ...);
_organizationRepository.Add(organization);
var saved = await _organizationRepository.UnitOfWork.SaveChangesAsync(ct);
```

`Organization.Create()` validates business rules, generates `TenantId`, and raises `OrganizationCreatedDomainEvent`.

### 5. DomainEventProcessorBehavior

After the handler returns, dispatches domain events within the active transaction. Cascade loop re-checks for new events.

### 6. Domain Event Handler

```csharp
public class OrganizationCreatedDomainEventHandler : IDomainEventHandler<OrganizationCreatedDomainEvent>
{
    public async Task Handle(OrganizationCreatedDomainEvent notification, CancellationToken ct)
    {
        var organization = await _repository.GetAsync(notification.OrganizationId);
        await _outboxService.AddAndSaveEventAsync(
            new OrganizationCreatedIntegrationEvent(...));
    }
}
```

### 7. Transaction Commit

`CommitTransactionAsync()` flushes OutboxMessages + audit logs, then commits. All data (Organization + IdempotencyEntry + OutboxMessage) committed atomically.

### 8. IntegrationEventProcessorBehavior

After commit, reads `LastCommittedTransactionId` from `ITransactionalUnitOfWork` and publishes outbox events to the event bus. If publishing fails, `OutboxProcessor` retries.

### 9. Consumer Services

**Identity Service**: `TelemetryDecorator` → `IdempotencyIntegrationEventEfDecorator` → Handler creates Tenant + Admin User within a transaction. Idempotency entry ensures at-most-once processing.

**Notifications Service**: Same decorator chain. Handler sends welcome email and sales notification.

---

## What Gets Committed Atomically

| Data | Table | Same Transaction |
|------|-------|:---:|
| Organization entity | `accounts.Organizations` | ✅ |
| Idempotency entry | `infra.IdempotencyEntry` | ✅ |
| Outbox message | `infra.OutboxMessage` | ✅ |
| Audit logs (if enabled) | `infra.AuditLog` | ✅ |

---

## Failure Impact

| Failure Point | Aggregate Persisted? | Outbox Persisted? | Event Published? |
|---|:---:|:---:|:---:|
| CommandHandler throws | ❌ | ❌ | ❌ |
| Domain event handler throws | ❌ | ❌ | ❌ |
| CommitAsync fails | ❌ | ❌ | ❌ |
| Event bus publish fails | ✅ | ✅ (status: Failed) | ❌ (OutboxProcessor retries) |
| Consumer handler throws | ✅ | ✅ (status: Published) | ✅ (consumer retries via event bus) |
