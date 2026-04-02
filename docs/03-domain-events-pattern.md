# Domain Events Pattern

## Executive Summary

### Brief Description
Domain Events represent state changes within a bounded context that other parts of the same bounded context need to react to. They enable loose coupling between aggregates while maintaining transactional consistency. Events are dispatched AFTER transaction commit by MessagingBehavior, ensuring data integrity and following DDD principles.

### Technical Implementation

**Location**: 
- Interface: `src/Qlt.Domain/Qlt.Domain.Core/SeedWork/IDomainEvent.cs` (pure DDD, no dependencies)
- Handler Interface: `src/Qlt.Platform/Qlt.Platform.Messaging.Abstractions/DomainEvents/IDomainEventHandler.cs` (MediatR adapter)
- Telemetry Behavior: `src/Qlt.Platform/Qlt.Platform.Messaging/Behaviors/TelemetryDomainEventBehavior.cs` (INotificationPublisher, see [doc 16](16-handler-telemetry-strategy.md))
- **Collector Interface**: `src/Qlt.Platform/Qlt.Platform.Messaging.EntityFramework/IDomainEventCollector.cs` (separates event collection from transactions)
- **Dispatcher Extension**: `src/Qlt.Platform/Qlt.Platform.Messaging.EntityFramework/Extensions/DomainEventsDispatcherExtension.cs` (centralized telemetry)
- **Behavior**: `src/Qlt.Platform/Qlt.Platform.Messaging.EntityFramework/Behaviors/MessagingBehavior.cs` (dispatches AFTER commit)
- **DbContext**: `src/Qlt.Platform/Qlt.Platform.Persistence.EntityFramework.Relational/QltDBContext.cs` (implements IDomainEventCollector, stores entities)
- Entity Base: `src/Qlt.Domain/Qlt.Domain.Core/SeedWork/Entity.cs`
- Example Handler: `src/Qlt.Services/Qlt.Services.Accounts/Application/DomainEventHandlers/OrganizationCreatedDomainEventHandler.cs`

**Core Components**:

1. **IDomainEvent Interface (Domain.Core - Pure DDD)**:
```csharp
// In Domain.Core (no dependencies on infrastructure)
namespace Qlt.Domain;

public interface IDomainEvent
{
    // Marker interface - intentionally empty
}
```

2. **IDomainEventHandler Interface (Platform - MediatR Adapter)**:
```csharp
// In Platform.Messaging.Abstractions (adapts Domain to MediatR)
using MediatR;
using Qlt.Domain;

namespace Qlt.Platform.Messaging.DomainEvents;

public interface IDomainEventHandler<TEvent> : INotificationHandler<TEvent>
    where TEvent : IDomainEvent, INotification
{
}
```

3. **Domain Event Definition**:
```csharp
// Events implement both IDomainEvent (Domain) and INotification (MediatR)
using MediatR;
using Qlt.Domain;

public record OrganizationCreatedDomainEvent(
    Guid OrganizationId,
    string Name,
    string TaxNumber) : IDomainEvent, INotification;
```

4. **Entity Domain Event Collection**:
```csharp
public abstract class Entity
{
    [NotMapped]
    [JsonIgnore]
    private List<IDomainEvent>? _domainEvents;
    
    public IReadOnlyCollection<IDomainEvent>? DomainEvents => _domainEvents?.AsReadOnly();

    public Entity AddDomainEvent(IDomainEvent eventItem)
    {
        _domainEvents = _domainEvents ?? new List<IDomainEvent>();
        _domainEvents.Add(eventItem);
        return this;
    }

    public void ClearDomainEvents() => _domainEvents?.Clear();
}
```

3. **IDomainEventCollector Interface** (Separation of Responsibilities):
```csharp
// Platform.Messaging.EntityFramework - Separates event collection from transaction management
public interface IDomainEventCollector
{
    IReadOnlyList<Entity> GetEntitiesWithPendingEvents();
}
```

4. **Domain Event Collection** (in QltDBContext - Stores Entities, Not Events):
```csharp
public async Task<bool> SaveEntitiesAsync(CancellationToken cancellationToken = default)
{
    if (_configuration.UseAuditLogs)
        await this.SaveAuditLogsAsync(cancellationToken);

    // Collect entities with domain events (keep events inside entities)
    _entitiesWithEvents = ChangeTracker
        .Entries<Entity>()
        .Where(x => x.Entity.DomainEvents != null && x.Entity.DomainEvents.Any())
        .Select(x => x.Entity)
        .ToList();

    // Save changes (INSERT/UPDATE entities)
    await base.SaveChangesAsync(cancellationToken);

    return true;
}

// IDomainEventCollector implementation
public IReadOnlyList<Entity> GetEntitiesWithPendingEvents()
{
    var entities = _entitiesWithEvents ?? new List<Entity>();
    _entitiesWithEvents = null;  // Clear after reading
    return entities;
}
```

**Key Improvement**: Events remain in entities until dispatch (more DDD pure). Events are extracted AFTER commit by the dispatcher extension.

5. **Domain Event Dispatcher Extension** (Centralized Telemetry):
```csharp
// Platform.Messaging.EntityFramework/Extensions/DomainEventsDispatcherExtension.cs
public static class DomainEventsDispatcherExtension
{
    public static async Task DispatchDomainEventsAsync(
        this IMediator mediator,
        IReadOnlyList<Entity> entities,
        CancellationToken cancellationToken = default)
    {
        using var activity = ActivityHelper.StartActivity("DispatchDomainEvents");

        // Extract events from entities
        var domainEvents = entities
            .SelectMany(e => e.DomainEvents ?? Array.Empty<IDomainEvent>())
            .ToList();

        // Clear events from entities
        foreach (var entity in entities)
            entity.ClearDomainEvents();

        // Dispatch with telemetry
        ApplicationDiagnostics.DomainEventsDispatched.Add(domainEvents.Count);

        foreach (var domainEvent in domainEvents)
        {
            await mediator.Publish(domainEvent, cancellationToken);
        }
    }
}
```

6. **Domain Event Dispatch** (in MessagingBehavior - AFTER commit):
```csharp
public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken ct)
{
    var response = await next(ct);  // Handler + Transaction executed

    if (_unitOfWork.LastCommittedTransactionId is { } transactionId)
    {
        // 1. Dispatch Domain Events FIRST (transaction already committed ✅)
        if (_eventCollector is not null)
        {
            var entities = _eventCollector.GetEntitiesWithPendingEvents();
            if (entities.Count > 0)
            {
                await _mediator.DispatchDomainEventsAsync(entities, ct);
            }
        }

        // 2. Publish Integration Events to event bus
        if (_outboxService is not null)
        {
            await _outboxService.PublishEventsThroughEventBusAsync(transactionId);
        }
    }

    return response;
}
```

**Architecture Highlights**:
- ✅ **SRP Preserved**: `ITransactionalUnitOfWork` only manages transactions, `IDomainEventCollector` only collects events
- ✅ **Layer Separation**: Dispatcher extension in `Platform.Messaging.EntityFramework` (not Persistence)
- ✅ **DDD Pure**: Events remain in entities until dispatch
- ✅ **Centralized Telemetry**: All dispatch goes through extension method with ActivitySource and metrics

7. **Example Usage**:
```csharp
// Domain Event Definition
public record OrganizationCreatedDomainEvent(
    Guid OrganizationId,
    string Name,
    string TaxNumber) : IDomainEvent;

// Raising event in aggregate
public class Organization : Entity, IAggregateRoot
{
    public static Organization Create(string name, string taxNumber, ...)
    {
        var organization = new Organization
        {
            Name = name,
            TaxNumber = taxNumber,
            // ...
        };

        organization.AddDomainEvent(new OrganizationCreatedDomainEvent(
            organization.Id,
            organization.Name,
            organization.TaxNumber));

        return organization;
    }
}

5. **Domain Event Handler (Pure Business Logic)**:
```csharp
using Microsoft.Extensions.Logging;
using Qlt.Platform.Messaging.DomainEvents;
using Qlt.Platform.Messaging.Outbox;
using Qlt.Services.Accounts.Domain.Events;

public class OrganizationCreatedDomainEventHandler 
    : IDomainEventHandler<OrganizationCreatedDomainEvent>
{
    private readonly ILogger<OrganizationCreatedDomainEventHandler> _logger;
    private readonly IOrganizationRepository _repository;
    private readonly IEventBusOutboxService _outboxService;

    public OrganizationCreatedDomainEventHandler(
        ILogger<OrganizationCreatedDomainEventHandler> logger,
        IOrganizationRepository repository,
        IEventBusOutboxService outboxService)
    {
        _logger = logger;
        _repository = repository;
        _outboxService = outboxService;
    }

    public async Task Handle(
        OrganizationCreatedDomainEvent notification, 
        CancellationToken ct)
    {
        _logger.LogInformation(
            "Organization created: {OrganizationId} - {Name}",
            notification.OrganizationId,
            notification.Name);

        // Verify entity is tracked
        var organization = await _repository.GetAsync(notification.OrganizationId);

        if (organization != null)
        {
            // Publish integration event to outbox
            var integrationEvent = new OrganizationCreatedIntegrationEvent(
                organization.TenantId,
                organization.Id,
                organization.Name,
                organization.LegalName,
                organization.TaxNumber,
                organization.Address?.CountryCode!
            );

            await _outboxService.AddAndSaveEventAsync(integrationEvent);
        }
    }
}
```

6. **Telemetry Decorator (Automatic via DI)**:
```csharp
// Automatically wraps all domain event handlers with telemetry
// Located in: Platform.Messaging/DomainEvents/TelemetryDomainEventHandlerDecorator.cs

public class TelemetryDomainEventHandlerDecorator<TDomainEvent> 
    : IDomainEventHandler<TDomainEvent>
    where TDomainEvent : IDomainEvent, INotification
{
    private readonly IDomainEventHandler<TDomainEvent> _inner;

    public async Task Handle(TDomainEvent notification, CancellationToken ct)
    {
        var eventType = typeof(TDomainEvent).Name;
        var handlerType = _inner.GetType().Name;

        using var activity = ActivityHelper.StartHandlerActivity($"Handle {eventType}");
        var stopwatch = Stopwatch.StartNew();

        try
        {
            await _inner.Handle(notification, ct); // Call actual handler
            stopwatch.Stop();

            MetricsHelper.RecordDomainEventHandlerExecution(
                eventType, handlerType, stopwatch.Elapsed.TotalMilliseconds, true);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            MetricsHelper.RecordDomainEventHandlerExecution(
                eventType, handlerType, stopwatch.Elapsed.TotalMilliseconds, false);
            ActivityHelper.RecordException(activity, ex);
            throw;
        }
    }
}
```

7. **DI Registration**:
```csharp
// In service Extensions.cs
services.AddMediatR(cfg =>
{
    cfg.RegisterServicesFromAssembly(typeof(AssemblyReference).Assembly);
    // Behaviors...
});

// Automatically decorate all domain event handlers with telemetry
services.AddDomainEventTelemetryDecorator();
```

### Architecture: Decorator Pattern

**Pattern Applied:** Decorator Pattern (Gang of Four)

**Why Decorator over Base Class?**
- ✅ **Safety**: Impossible to accidentally bypass telemetry (no method to override incorrectly)
- ✅ **Consistency**: Same pattern as Integration Events
- ✅ **Extensibility**: Can chain multiple decorators (retry, caching, etc.)
- ✅ **Testability**: Handlers are pure and can be tested without telemetry infrastructure
- ✅ **Single Responsibility**: Handler = business logic, Decorator = cross-cutting concern
- ✅ **Composition over Inheritance**: Aligns with SOLID principles

**Previous Approach (Removed):**
~~Base class `DomainEventHandler<T>` with `HandleAsync` abstract method~~ → Risky (developers could override `Handle()` by mistake)

**Current Approach:**
Direct implementation of `IDomainEventHandler<T>` + automatic decorator via DI

### Critical Points Detected

> **Architectural Note**: Domain events are dispatched **within the same database transaction** as the command handler.
> This is an **intentional design decision** based on the eShop reference architecture (Microsoft) and
> *Patterns of Enterprise Application Architecture* (Martin Fowler). It provides strong consistency within
> bounded contexts and eliminates the need for compensation/saga mechanisms for local domain events.
> See [doc 17](17-developer-guide-transactional-pipeline.md) for developer considerations.

1. **✅ Design Decision: Events Dispatched Within Transaction**:
   - Domain events are dispatched AFTER the command handler's `SaveChangesAsync()` but BEFORE `CommitAsync()`
   - All domain event handlers participate in the same transaction
   - If any handler fails, the entire transaction rolls back (aggregate + outbox + idempotency)
   - **Benefit**: Atomic consistency — aggregate state and side effects commit or fail together
   - **Trade-off**: Transaction duration increases with each handler. Keep handlers lightweight.

2. **✅ Cascading Event Support**:
   - `DomainEventProcessorBehavior` re-checks for new domain events after each dispatch cycle
   - Handlers that modify aggregates raising new events are handled in the same transaction
   - **Benefit**: Complex domain workflows in a single atomic operation

3. **⚠️ No Event Ordering Guarantee**:
   - Events collected via `SelectMany()` with no ordering
   - Order depends on EF ChangeTracker enumeration order
   - **Risk**: Medium - Handler execution order non-deterministic
   - **Impact**: Medium - Dependent handlers may execute out of order

4. **⚠️ Repository Query in Domain Event Handler**:
   - Handler re-queries the repository: `await _repository.GetAsync(notification.OrganizationId)`
   - Entity is available because the command handler already called `SaveChangesAsync()` (flushed within the transaction)
   - **Risk**: Low - Performance overhead (extra SELECT)
   - **Consideration**: Can pass aggregate data via the event record instead

5. **✅ Events Not Persisted**:
   - Domain events are in-memory only, dispatched synchronously within the transaction
   - Good for consistency (same transaction)
   - Events for external systems go to Outbox (persisted atomically)

6. **✅ OpenTelemetry Integration**:
   - Domain event publishing tracked with metrics
   - Activity spans for distributed tracing
   - Good observability

### Alternative Approach: Post-Commit Dispatch

The pipeline is designed so that **changing the behavior order** is the only modification needed to switch to post-commit dispatch. Moving `DomainEventProcessorBehavior` before `TransactionBehavior` in the registration order causes events to dispatch after the transaction commits. This approach was evaluated and intentionally rejected because:

- Requires compensation/retry mechanisms for handler failures
- Requires idempotency for domain event handlers
- Adds infrastructure complexity without proportional benefit for our use case
- Domain event handlers adding outbox messages would fail (no active transaction)

The single-transaction approach was chosen for simplicity and correctness. The pipeline remains **prepared** for the alternative if a future bounded context requires it.

## Conclusion

The Domain Events implementation follows an intentional architectural pattern where events are dispatched **within the same database transaction** as the command handler. This is based on the eShop reference architecture and *Patterns of Enterprise Application Architecture*, providing:

- ✅ **Strong consistency** within bounded contexts (no eventual consistency needed locally)
- ✅ **Atomic rollback** if any handler fails
- ✅ **No compensation infrastructure** needed for local domain events
- ✅ **Consistency between bounded contexts** via Transactional Outbox (eventual consistency)
- ✅ **Flexibility** — switching to post-commit dispatch requires only changing behavior registration order

**Considerations for developers**:
1. 🟡 Keep domain event handlers lightweight (minimize transaction duration)
2. 🟡 Heavy processing should be delegated to integration events (async, outside transaction)
3. 🟢 Event ordering is not guaranteed — handlers should be independent
4. 🟢 Pass data via event records when possible to avoid re-querying

**Overall Architecture**: ✅ Intentional design — strong consistency within bounded contexts, eventual consistency between them.

The pattern is conceptually correct, but the timing of event dispatch relative to transaction commit needs immediate attention to prevent data consistency issues.
