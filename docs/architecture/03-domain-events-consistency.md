# Domain Events & Consistency

Covers domain events, the single-transaction model, and the consistency boundaries between bounded contexts.

---

## Domain Events

Domain events represent state changes within a bounded context. They enable loose coupling between aggregates while maintaining transactional consistency.

### Definition

```csharp
// Domain.Core — pure DDD, no infrastructure dependencies
public interface IDomainEvent { }

// Platform.Messaging.Abstractions — MediatR adapter
public interface IDomainEventHandler<TEvent> : INotificationHandler<TEvent>
    where TEvent : IDomainEvent, INotification { }
```

Events implement both `IDomainEvent` (domain concept) and `INotification` (MediatR transport):

```csharp
public record OrganizationCreatedDomainEvent(
    Guid OrganizationId,
    string Name,
    string TaxNumber) : IDomainEvent, INotification;
```

### Raising Events

Entities raise domain events through the `Entity` base class:

```csharp
public static Organization Create(string name, ...)
{
    var organization = new Organization { Name = name };
    organization.AddDomainEvent(new OrganizationCreatedDomainEvent(
        organization.Id, organization.Name, organization.TaxNumber));
    return organization;
}
```

Events are stored in-memory on the entity until dispatched.

### Dispatching Events

`DomainEventProcessorBehavior` dispatches domain events **within the active transaction**, after `SaveChangesAsync()` but before `CommitAsync()`. This means handlers can:

- Query persisted entities with database-generated IDs
- Modify aggregates (changes are part of the same transaction)
- Add integration events to the outbox

The behavior uses `IDomainEntityChangeTracker` to find entities with pending events, then `DomainEventsDispatcherExtension` (in `Platform.Messaging`) to publish them via MediatR.

**Cascading support:** After dispatching, the behavior re-checks for new domain events raised by handlers. This loop continues until no new events remain.

### Telemetry

`TelemetryDomainEventBehavior` is a custom `INotificationPublisher` that wraps each domain event handler invocation with OpenTelemetry activities and metrics. It replaces MediatR's default publisher.

---

## Single-Transaction Model

All changes within a command are atomic: aggregate state, domain event side-effects, idempotency entry, and outbox messages are committed in a single database transaction.

### Transaction Flow

```
TransactionBehavior
  ├── BEGIN TRANSACTION
  │
  ├── CommandHandler
  │     └── Create aggregate, raise domain events
  │     └── SaveChangesAsync() — persist aggregate, generate IDs
  │
  ├── DomainEventProcessorBehavior
  │     └── Dispatch domain events (handlers add outbox messages)
  │     └── Re-check for cascading events
  │
  ├── COMMIT TRANSACTION
  │     └── Aggregate + IdempotencyEntry + OutboxMessages committed atomically
  │
  └── Set LastCommittedTransactionId

IntegrationEventProcessorBehavior
  └── Read LastCommittedTransactionId
  └── Publish outbox events to event bus (AFTER commit)
```

### Why Two SaveChanges Within One Transaction

The command handler calls `SaveChangesAsync()` to flush the aggregate to the database. This is necessary so that domain event handlers can:

1. **Query the persisted entity** with database-generated IDs
2. **Access computed values** (defaults, sequences)
3. **Add outbox messages** that reference the entity

The domain event handlers add their changes to the EF change tracker. When `CommitTransactionAsync()` is called, EF performs a final `SaveChangesAsync()` before committing the transaction — persisting outbox messages and any handler modifications atomically.

---

## Consistency Boundaries

### Strong Consistency (Within Bounded Context)

Everything within a single command runs in **one database transaction**:

```
Accounts Service (single transaction)
├── Create Organization (aggregate)
├── Save to DB (first SaveChanges)
├── Dispatch OrganizationCreatedDomainEvent
│     └── Handler saves integration event to outbox
├── COMMIT (aggregate + outbox = atomic)
└── Publish integration event to message broker
```

### Eventual Consistency (Across Bounded Contexts)

Integration events are published asynchronously via the message broker:

```
Accounts Service                    Identity Service
┌─────────────────┐                ┌─────────────────┐
│ Strong           │                │ Eventual         │
│ Consistency      │   Message      │ Consistency      │
│                  │   Broker       │                  │
│ Commit TX ──────►│──────────────►│ Receive Event    │
│                  │  (RabbitMQ/   │ Check Idempotency│
│                  │   ServiceBus) │ Process Event    │
│                  │                │ Commit TX        │
└─────────────────┘                └─────────────────┘
```

The receiving service processes the event in its own transaction with idempotent consumer protection.

### Domain Event Handler Example

```csharp
public class OrganizationCreatedDomainEventHandler : IDomainEventHandler<OrganizationCreatedDomainEvent>
{
    public async Task Handle(OrganizationCreatedDomainEvent notification, CancellationToken ct)
    {
        var organization = await _repository.GetAsync(notification.OrganizationId);

        var integrationEvent = new OrganizationCreatedIntegrationEvent(
            organization!.Id, organization.Name, ...);

        await _eventBusOutboxService.AddAndSaveEventAsync(integrationEvent);
    }
}
```

The handler queries the persisted organization (available because `SaveChangesAsync()` already ran) and saves an integration event to the outbox (committed with the same transaction).

---

## Design Decisions

**Why dispatch domain events WITHIN the transaction (not after)?** Handlers need access to persisted data and must be able to modify the database atomically. If dispatch happened after commit, handler failures would leave the system in an inconsistent state.

**Why a separate DomainEventProcessorBehavior instead of dispatching in SaveChanges?** Separating dispatch from persistence follows SRP. The behavior is configurable independently and keeps `QltDBContext` focused on persistence.

**Why cascading re-check?** Domain event handlers may raise additional domain events. The re-check loop ensures all cascading events are processed within the same transaction.

**Why IDomainEntityChangeTracker separate from ITransactionalUnitOfWork?** SRP — transaction management and entity tracking are distinct responsibilities. `ITransactionalUnitOfWork` handles transactions; `IDomainEntityChangeTracker` exposes tracked entities for event dispatch.
