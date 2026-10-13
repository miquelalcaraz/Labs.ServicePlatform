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
// Accounts.Domain/Events/CompanyCreatedDomainEvent.cs
public record CompanyCreatedDomainEvent(
    Guid TenantId,
    Guid CompanyId,
    string LegalName,
    string TradeName,
    string TaxId,
    string CountryCode) : IDomainEvent, INotification;
```

### Raising Events

Entities raise domain events through the `Entity` base class (`AddDomainEvent`, `RemoveDomainEvent`, `ClearDomainEvents`):

```csharp
// Accounts.Domain/Models/CompanyAggregate/Company.cs
public static Company Create(Guid tenantId, string legalName, /* ... */)
{
    var company = new Company { /* ... */ Status = CompanyStatus.Pending };
    company.AddCompanyCreatedDomainEvent();
    return company;
}

private void AddCompanyCreatedDomainEvent()
{
    AddDomainEvent(new CompanyCreatedDomainEvent(
        TenantId, Id, LegalName, TradeName, TaxId, Address?.CountryCode!));
}
```

Events are stored in-memory on the entity (`[NotMapped]`, `[JsonIgnore]`) until dispatched. Because `Entity` generates its `Guid` identity in the constructor, events can carry the aggregate ID before anything is persisted.

### Dispatching Events

`DomainEventProcessorBehavior` dispatches domain events **within the active transaction**, after the handler returns (and after its `SaveChangesAsync()`), but before `TransactionBehavior` commits. This means handlers can:

- Query persisted entities through repositories
- Modify aggregates (changes are part of the same transaction)
- Add integration events to the outbox

```mermaid
flowchart TB
    A["Handler returns"] --> B{"Transaction id available?<br/>current tx ?? LastCommittedTransactionId"}
    B -- no --> Z["return response"]
    B -- yes --> C["entities = changeTracker.GetEntitiesWithChanges()<br/>where DomainEvents not empty"]
    C --> D{"entities.Count #gt; 0?"}
    D -- no --> Z
    D -- yes --> E["mediator.DispatchDomainEventsAsync(entities)<br/>1 · collect events<br/>2 · clear events on entities<br/>3 · Publish each event sequentially"]
    E --> F["handlers may raise new domain events"]
    F --> C
```

The behavior uses `IDomainEntityChangeTracker` to find entities with pending events and `DomainEventsDispatcherExtension` (in `Platform.Messaging`) to publish them via MediatR. Events are cleared **before** publishing, so a handler that raises new events on the same entity starts a new cycle instead of re-dispatching old ones.

**Cascading support:** After each cycle, the behavior re-reads the change tracker. The loop continues until no entity has pending events. The number of cycles and entities is recorded on the `DomainEventProcessor {Command}` activity.

### Telemetry

`TelemetryDomainEventBehavior` is a custom `INotificationPublisher` that executes handlers **sequentially** and wraps each `IDomainEventHandler<>` invocation with an OpenTelemetry activity and metrics. It replaces MediatR's default publisher (see [06-telemetry.md](06-telemetry.md)).

---

## Single-Transaction Model

All changes within a command are atomic: aggregate state, domain event side-effects, idempotency entry, and outbox messages are committed in a single database transaction.

### Transaction Flow

```mermaid
sequenceDiagram
    autonumber
    participant TX as TransactionBehavior
    participant H as CreateCompanyCommandHandler
    participant DEP as DomainEventProcessorBehavior
    participant DEH as CompanyCreatedDomainEventHandler
    participant OB as EventBusOutboxService
    participant DB as AccountContext
    participant IEP as IntegrationEventProcessorBehavior

    TX->>DB: BEGIN TRANSACTION
    TX->>H: next()
    H->>DB: Add(company) + SaveChangesAsync()
    Note over DB: Company + IdempotencyEntry flushed (not committed)
    H-->>DEP: Result.Success(true)
    DEP->>DEH: Publish(CompanyCreatedDomainEvent)
    DEH->>DB: GetAsync(companyId)
    DEH->>OB: AddAndSaveEventAsync(CompanyCreatedIntegrationEvent)
    OB->>DB: Add OutboxMessage (TransactionId = current tx)
    DEP->>DEP: re-check for cascading events (none)
    DEP-->>TX: response
    TX->>DB: CommitTransactionAsync()
    Note over DB: final SaveChanges + COMMIT<br/>Company + IdempotencyEntry + OutboxMessage are atomic
    DB-->>TX: LastCommittedTransactionId
    TX-->>IEP: response
    IEP->>OB: PublishEventsThroughEventBusAsync(transactionId) — AFTER commit
```

### Why Two SaveChanges Within One Transaction

The command handler calls `SaveChangesAsync()` to flush the aggregate to the database. This is useful so that domain event handlers can:

1. **Query the persisted entity** through repositories
2. **Access computed values** (database defaults, `RowVersion`, audit timestamps)
3. **Add outbox messages** that reference the entity

The domain event handlers add their changes to the EF change tracker. `CommitTransactionAsync()` applies audit timestamps and performs a final `SaveChangesAsync()` before committing — persisting outbox messages and any handler modifications atomically.

---

## Consistency Boundaries

### Strong Consistency (Within Bounded Context)

Everything within a single command runs in **one database transaction**:

```mermaid
flowchart LR
    subgraph TX["Accounts — single transaction"]
        direction TB
        A["Create Company (aggregate)"] --> B["SaveChanges (flush)"]
        B --> C["Dispatch CompanyCreatedDomainEvent"]
        C --> D["Handler adds CompanyCreatedIntegrationEvent to outbox"]
        D --> E["COMMIT (aggregate + idempotency + outbox)"]
    end
    E --> F["Publish to event bus (after commit)"]
```

### Eventual Consistency (Across Bounded Contexts)

Integration events are published asynchronously through the event bus (Azure Service Bus topic `sirius-event-bus` by default). Each receiving service processes the event in its own transaction behind the idempotent consumer decorator.

Real integration event flows between services:

```mermaid
flowchart LR
    ACC["Accounts"]
    IDN["Identity"]
    NOT["Notifications"]

    ACC -- "CompanyCreated" --> NOT
    ACC -- "ActivationEmailRequested" --> NOT
    ACC -- "TenantCreated" --> IDN
    ACC -- "CustomerAdminProvisioningRequested" --> IDN
    IDN -- "ExternalUserProvisioned" --> ACC
    IDN -- "UserActivated" --> ACC
    NOT -- "ActivationDelivered" --> ACC
```

`CustomerActivationCompletedIntegrationEvent` and `CustomerGoLiveIntegrationEvent` are published by Accounts but currently have no subscribers.

```mermaid
sequenceDiagram
    participant ACC as Accounts
    participant BUS as Event Bus
    participant IDN as Identity

    Note over ACC: Strong consistency
    ACC->>ACC: COMMIT (business data + outbox)
    ACC->>BUS: Publish TenantCreated
    Note over BUS,IDN: Eventual consistency
    BUS->>IDN: Deliver message
    IDN->>IDN: Idempotency check (event id + handler name)
    IDN->>IDN: BEGIN → handle → mark processed → COMMIT
    IDN-->>BUS: Complete message
```

### Integration Event → Command (Nested Transaction)

Integration event handlers are thin translators that send an application command through MediatR:

```csharp
// Accounts.Infrastructure/IntegrationEvents/EventHandling/UserActivatedIntegrationEventHandler.cs
public async Task Handle(UserActivatedIntegrationEvent @event)
{
    var result = await _mediator.Send(new CompleteActivationOnUserActivatedCommand(@event.UserId));

    if (result.IsFailure)
        _logger.LogWarning("CompleteActivationOnUserActivated failed for UserId={UserId}: {Error}",
            @event.UserId, result.Error.Message);
}
```

`IdempotencyIntegrationEventEfDecorator` opens the transaction before calling the handler, so the command runs **nested**:

```mermaid
sequenceDiagram
    participant DEC as IdempotencyIntegrationEventEfDecorator
    participant H as IntegrationEventHandler
    participant TX as TransactionBehavior
    participant DEP as DomainEventProcessorBehavior
    participant IEP as IntegrationEventProcessorBehavior
    participant DB as Write DbContext
    participant OP as OutboxProcessor

    DEC->>DB: BEGIN TRANSACTION
    DEC->>H: Handle(event)
    H->>IEP: mediator.Send(command)
    IEP->>TX: next()
    TX->>TX: HasActiveTransaction → nested, pass through
    TX->>DEP: next() → handler + SaveChanges
    DEP->>DB: domain events add OutboxMessages (tracked, not flushed)
    IEP->>DB: query pending outbox rows for current tx → none flushed yet
    H-->>DEC: done
    DEC->>DB: Add IdempotencyEntry + SaveChanges + COMMIT
    Note over DB,OP: Outbox rows are committed as NotPublished
    OP->>DB: next polling cycle claims and publishes them
```

In this flow the outbox events are committed atomically with the business data and the idempotency entry, but they are published by the `OutboxProcessor` on its next polling cycle (default every 30 s) rather than inline.

### Identity: Two Contexts

Identity keeps ASP.NET Identity / Duende data in `IdentityContext` and the platform infrastructure (idempotency + outbox) in `IdentityLogsContext : SiriusDBContext`. Command handlers such as `ProvisionCustomerAdminCommandHandler` write users and tenants through `ResilientTransaction.New(_identityContext)` and then add the integration event to the outbox of `IdentityLogsContext`. These are **two separate database transactions**, so Identity relies on idempotent "create if not exists" logic instead of a single atomic commit.

### Domain Event Handler Example

```csharp
// Accounts.Application/DomainEventHandlers/CompanyCreatedDomainEventHandler.cs
public async Task Handle(CompanyCreatedDomainEvent notification, CancellationToken cancellationToken)
{
    var company = await _companyRepository.GetAsync(notification.CompanyId);

    if (company != null)
    {
        var integrationEvent = new CompanyCreatedIntegrationEvent(
            company.TenantId, company.Id, company.LegalName,
            company.TradeName, company.TaxId, company.Address?.CountryCode!);

        await _outboxService.AddAndSaveEventAsync(integrationEvent);
    }
    else
    {
        _logger.LogWarning("Company {CompanyId} not found ... Integration event not published.", notification.CompanyId);
    }
}
```

The handler loads the persisted company and adds an integration event to the outbox, committed with the same transaction. `AddAndSaveEventAsync` throws `InvalidOperationException` if no transaction is active.

---

## Design Decisions

**Why dispatch domain events WITHIN the transaction (not after)?** Handlers need access to persisted data and must be able to modify the database atomically. If dispatch happened after commit, handler failures would leave the system in an inconsistent state.

**Why a separate DomainEventProcessorBehavior instead of dispatching in SaveChanges?** Separating dispatch from persistence follows SRP. The behavior is configurable independently and keeps `SiriusDBContext` focused on persistence.

**Why cascading re-check?** Domain event handlers may raise additional domain events. The re-check loop ensures all cascading events are processed within the same transaction.

**Why IDomainEntityChangeTracker separate from ITransactionalUnitOfWork?** SRP — transaction management and entity tracking are distinct responsibilities. `ITransactionalUnitOfWork` handles transactions; `IDomainEntityChangeTracker` exposes tracked entities for event dispatch.

**Why do integration event handlers send commands?** The handler stays a thin adapter; business rules, validation, idempotency and domain events live in the application command, reusing the same pipeline as API requests.
