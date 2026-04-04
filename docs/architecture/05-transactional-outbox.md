# Transactional Outbox

## Overview

The Transactional Outbox pattern guarantees that integration events are published reliably alongside business data changes. Business data and outbox entries are saved in the **same database transaction**, ensuring atomicity. Events are then published to the event bus after commit, with a background processor handling retries for any failures.

**Layer**: `Platform.Messaging.EntityFramework`  
**Key types**: `OutboxMessageService<TContext>`, `EventBusOutboxService<TContext>`, `OutboxProcessor`, `IntegrationEventProcessorBehavior<TRequest, TResponse>`

---

## Domain Model

`OutboxMessage` is a persistence-ignorant POCO defined in `Platform.Messaging`:

```csharp
public class OutboxMessage
{
    public Guid Id { get; set; }
    public string TypeName { get; set; }
    public string Payload { get; set; }
    public OutboxMessageStatus Status { get; set; }
    public int SendCount { get; set; }
    public DateTime CreatedAt { get; set; }
    public Guid TransactionId { get; set; }
    public byte[] RowVersion { get; set; }
}

public enum OutboxMessageStatus
{
    NotPublished,
    InProgress,
    Published,
    FailedToPublish
}
```

Entity configuration lives in `Platform.Persistence.EntityFramework.Relational` (`OutboxMessageConfiguration`), following the 5-layer boundary rules.

---

## Services

### OutboxMessageService&lt;TContext&gt;

Low-level service that persists and retrieves outbox entries using EF Core:

| Method | Responsibility |
|--------|---------------|
| `SaveEventAsync` | Serializes an `IntegrationEvent` and inserts an `OutboxMessage` within the current transaction |
| `RetrieveEventLogsPendingToPublishAsync` | Queries outbox entries by `TransactionId` with status `NotPublished` |
| `MarkEventAsInProgressAsync` | Updates status to `InProgress`, increments `SendCount` |
| `MarkEventAsPublishedAsync` | Updates status to `Published` |
| `MarkEventAsFailedAsync` | Updates status to `FailedToPublish` |

### EventBusOutboxService&lt;TContext&gt;

Higher-level service that coordinates outbox persistence with event bus publishing:

```
AddAndSaveEventAsync(event)
  ├─ Validate active transaction exists
  └─ Delegate to OutboxMessageService.SaveEventAsync()

PublishEventsThroughEventBusAsync(transactionId)
  ├─ Retrieve pending events for transaction
  └─ For each event:
       ├─ MarkAsInProgress
       ├─ IEventBus.PublishAsync(deserializedEvent)
       ├─ Success → MarkAsPublished
       └─ Failure → MarkAsFailed (retry by OutboxProcessor)
```

---

## Pipeline Integration

### IntegrationEventProcessorBehavior

Runs **outside** the transaction. After the inner pipeline (including `TransactionBehavior`) commits, it publishes outbox events:

```
IntegrationEventProcessorBehavior (OUTSIDE transaction)
  └─ TransactionBehavior (BEGIN / COMMIT)
        └─ DomainEventProcessorBehavior (INSIDE transaction)
              └─ CommandHandler
```

```csharp
public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken ct)
{
    var response = await next(ct);

    if (_unitOfWork.LastCommittedTransactionId is { } transactionId)
    {
        await _outboxService.PublishEventsThroughEventBusAsync(transactionId);
    }

    return response;
}
```

### Domain Event Handlers → Outbox

Domain event handlers run inside the transaction and save integration events to the outbox atomically:

```csharp
public class OrganizationCreatedDomainEventHandler : IDomainEventHandler<OrganizationCreatedDomainEvent>
{
    public async Task Handle(OrganizationCreatedDomainEvent notification, CancellationToken ct)
    {
        var integrationEvent = new OrganizationCreatedIntegrationEvent(...);
        await _outboxService.AddAndSaveEventAsync(integrationEvent);
    }
}
```

---

## Background Processor

`OutboxProcessor` is a `BackgroundService` that retries failed or stuck events:

- **Polling**: Configurable interval via `OutboxProcessorOptions`
- **Batch processing**: Retrieves events with status `NotPublished` or `FailedToPublish` (below max retries), and `InProgress` events older than a timeout threshold
- **Retry**: Exponential backoff with configurable max retries
- **Telemetry**: OpenTelemetry activities and metrics for each batch cycle

---

## Concurrency Control

When multiple `OutboxProcessor` instances run concurrently (e.g., scaled-out services), race conditions can cause duplicate publishing. The solution uses an **atomic claim pattern**:

### Three-Step Atomic Claim

1. **Claim**: `ExecuteUpdateAsync` atomically sets `Status = InProgress` only for rows where `Status = NotPublished` and `RowVersion` matches
2. **Publish**: Send claimed events to the event bus
3. **Finalize**: Mark as `Published` or `FailedToPublish`

```csharp
var claimed = await context.Set<OutboxMessage>()
    .Where(e => e.Status == OutboxMessageStatus.NotPublished && e.Id == target.Id)
    .ExecuteUpdateAsync(s => s
        .SetProperty(e => e.Status, OutboxMessageStatus.InProgress)
        .SetProperty(e => e.SendCount, e => e.SendCount + 1));
```

### RowVersion Optimistic Concurrency

`OutboxMessage.RowVersion` is configured as a concurrency token in `OutboxMessageConfiguration`. If two processors try to claim the same row, only one succeeds — the other gets zero rows affected from `ExecuteUpdateAsync`.

This approach is **database-agnostic** — it uses standard EF Core API without raw SQL.

---

## Event Type Registration

All integration event types must be registered in `OutboxMessageOptions.EventTypes` for correct deserialization:

```csharp
builder.AddServiceBusEventBus("messaging")
    .AddSubscription<OrganizationCreatedIntegrationEvent, OrganizationCreatedIntegrationEventHandler>();
```

Unregistered event types cause a runtime deserialization failure when the outbox attempts to reconstruct the event.

---

## Telemetry

Both `OutboxMessageService` and `OutboxProcessor` emit:

| Signal | Details |
|--------|---------|
| Activities | `outbox.save`, `outbox.publish`, `outbox.process-batch` |
| Metrics | Batch size, event state transitions, operation duration, success/failure counts |

---

## Validation Checklist

- [ ] Domain event handlers save integration events via `IEventBusOutboxService.AddAndSaveEventAsync` (inside transaction)
- [ ] `IntegrationEventProcessorBehavior` is registered and runs outside `TransactionBehavior`
- [ ] `OutboxProcessor` background service is registered for retry of failed events
- [ ] All integration event types are registered in `OutboxMessageOptions.EventTypes`
- [ ] `OutboxMessageConfiguration` configures `RowVersion` as concurrency token
- [ ] No direct `IEventBus.PublishAsync` calls from domain event handlers (use outbox)
