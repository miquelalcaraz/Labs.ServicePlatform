# Domain Events Pattern

## Executive Summary

### Brief Description
Domain Events represent state changes within a bounded context that other parts of the same bounded context need to react to. They enable loose coupling between aggregates while maintaining transactional consistency. Events are dispatched AFTER transaction commit by MessagingBehavior, ensuring data integrity and following DDD principles.

### Technical Implementation

**Location**: 
- Interface: `src/Qlt.Domain/Qlt.Domain.Core/SeedWork/IDomainEvent.cs` (pure DDD, no dependencies)
- Handler Interface: `src/Qlt.Platform/Qlt.Platform.Messaging.Abstractions/DomainEvents/IDomainEventHandler.cs` (MediatR adapter)
- Telemetry Decorator: `src/Qlt.Platform/Qlt.Platform.Messaging/DomainEvents/TelemetryDomainEventHandlerDecorator.cs`
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

1. **🔴 Critical: Events Dispatched Inside Transaction**:
   - Domain events are dispatched AFTER `SaveChanges()` but BEFORE `CommitAsync()`
   - If domain event handler throws, transaction rolls back BUT data was already saved to DbContext
   - **Risk**: HIGH - Inconsistent state if domain event handler fails
   - **Example**: Organization saved to DB, but integration event handler fails → transaction rolls back but entity ID generated
   - **Impact**: CRITICAL - Can cause data inconsistency and duplicate key violations on retry

2. **⚠️ Domain Events Cleared Before Dispatch**:
   - Events are collected, then cleared from entities, then published
   - If dispatch fails mid-way, some events published, some not
   - **Risk**: Medium - Partial event processing without recovery mechanism
   - **Impact**: Medium - Some handlers execute, others don't

3. **⚠️ No Event Ordering Guarantee**:
   - Events collected via `SelectMany()` with no ordering
   - Order depends on EF ChangeTracker enumeration order
   - **Risk**: Medium - Handler execution order non-deterministic
   - **Impact**: Medium - Dependent handlers may execute out of order

4. **⚠️ Repository Query in Domain Event Handler**:
   - Handler re-queries the repository: `await _repository.GetAsync(notification.OrganizationId)`
   - Unnecessary database round-trip since entity already in memory
   - **Risk**: Low - Performance overhead
   - **Impact**: Low - Extra SELECT query

5. **✅ Events Not Persisted**:
   - Domain events are in-memory only, dispatched synchronously
   - Good for consistency (same transaction)
   - Events for external systems go to Outbox

6. **⚠️ No Retry Mechanism**:
   - If domain event handler fails, entire transaction fails
   - No retry or compensating action
   - **Risk**: Medium - Transient failures cause complete rollback
   - **Impact**: Medium - User operations fail due to infrastructure issues

7. **⚠️ Tight Coupling to EF Entity Framework**:
   - Dispatcher tightly coupled to `DbContext.ChangeTracker`
   - Only works with EF-tracked entities
   - **Risk**: Low - Acceptable for EF-based architecture
   - **Impact**: Low - Cannot use with other ORMs

8. **✅ OpenTelemetry Integration**:
   - Domain event publishing tracked with metrics
   - Activity spans for distributed tracing
   - Good observability

### Possible Improvements

1. **🔥 CRITICAL: Move Domain Event Dispatch AFTER Transaction Commit**:
```csharp
public async Task<bool> SaveEntitiesAsync(CancellationToken cancellationToken = default)
{
    // Save changes (do NOT dispatch events yet)
    await base.SaveChangesAsync(cancellationToken);
    
    // Return true - commit happens in TransactionBehavior
    return true;
}

// In TransactionBehavior, AFTER commit:
public async Task<TResponse> Handle(...)
{
    await using var transaction = await _dbContext.BeginTransactionAsync();
    
    var response = await next(cancellationToken); // Saves entities
    
    await _dbContext.CommitTransactionAsync(transaction); // Commit first
    
    // NOW dispatch domain events (outside transaction)
    await _mediator.DispatchDomainEventsAsync(_dbContext, cancellationToken);
    
    // Then publish integration events
    if (_integrationEventService != null)
        await _integrationEventService.PublishEventsThroughEventBusAsync(transactionId);
        
    return response;
}
```
**Pros**: Domain event handlers cannot break transaction
**Cons**: Eventual consistency within bounded context (handlers see committed data but execute after)

2. **Add Event Ordering**:
```csharp
public abstract class Entity
{
    private List<(int Order, IDomainEvent Event)>? _domainEvents;

    public Entity AddDomainEvent(IDomainEvent eventItem, int order = 0)
    {
        _domainEvents = _domainEvents ?? new List<(int, IDomainEvent)>();
        _domainEvents.Add((order, eventItem));
        return this;
    }
}

// In dispatcher:
var domainEvents = domainEntities
    .SelectMany(x => x.Entity.DomainEvents!)
    .OrderBy(e => e.Order)
    .ThenBy(e => e.Event.CreatedAt) // if events have timestamp
    .Select(e => e.Event)
    .ToList();
```

3. **Add Domain Event Store** (for debugging and audit):
```csharp
public class DomainEventStore
{
    public Guid Id { get; set; }
    public string EventType { get; set; }
    public string EventData { get; set; }
    public DateTime OccurredAt { get; set; }
    public Guid AggregateId { get; set; }
    public string AggregateType { get; set; }
    public bool Processed { get; set; }
}

// In dispatcher:
foreach (var domainEvent in domainEvents)
{
    // Store for audit trail
    await _eventStore.SaveAsync(new DomainEventStore
    {
        EventType = domainEvent.GetType().Name,
        EventData = JsonSerializer.Serialize(domainEvent),
        OccurredAt = DateTime.UtcNow,
        Processed = false
    });

    await mediator.Publish(domainEvent, ct);
    
    // Mark as processed
    await _eventStore.MarkProcessedAsync(eventId);
}
```

4. **Add Retry Policy for Domain Event Handlers**:
```csharp
public class DomainEventDispatcherWithRetry
{
    private readonly IMediator _mediator;
    private readonly ILogger _logger;

    public async Task DispatchAsync(IDomainEvent domainEvent, CancellationToken ct)
    {
        var retryPolicy = Policy
            .Handle<Exception>()
            .WaitAndRetryAsync(3, retryAttempt => 
                TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)));

        await retryPolicy.ExecuteAsync(async () =>
        {
            await _mediator.Publish(domainEvent, ct);
        });
    }
}
```

5. **Pass Aggregate Root Instead of ID to Handlers**:
```csharp
// Instead of:
public record OrganizationCreatedDomainEvent(Guid OrganizationId, string Name) : IDomainEvent;

// Use:
public record OrganizationCreatedDomainEvent(Organization Organization) : IDomainEvent;

// Handler no longer needs to query:
public async Task Handle(OrganizationCreatedDomainEvent notification, CancellationToken ct)
{
    var organization = notification.Organization; // Direct access, no query
    
    var integrationEvent = new OrganizationCreatedIntegrationEvent(
        organization.TenantId,
        organization.Id,
        organization.Name);
    
    await _outboxService.AddAndSaveEventAsync(integrationEvent);
}
```

6. **Add Domain Event Middleware/Pipeline**:
```csharp
public interface IDomainEventBehavior<TEvent> where TEvent : IDomainEvent
{
    Task Handle(TEvent @event, Func<Task> next, CancellationToken ct);
}

// Logging behavior
public class DomainEventLoggingBehavior<TEvent> : IDomainEventBehavior<TEvent>
{
    public async Task Handle(TEvent @event, Func<Task> next, CancellationToken ct)
    {
        _logger.LogInformation("Handling domain event {EventType}", typeof(TEvent).Name);
        
        await next();
        
        _logger.LogInformation("Completed domain event {EventType}", typeof(TEvent).Name);
    }
}
```

7. **Add Idempotency for Domain Event Handlers** (if moved after commit):
```csharp
public interface IIdempotentDomainEventHandler<TEvent> : IDomainEventHandler<TEvent>
    where TEvent : IDomainEvent
{
    string HandlerName { get; }
}

// In dispatcher:
if (handler is IIdempotentDomainEventHandler<TEvent> idempotentHandler)
{
    var eventId = domainEvent.Id; // Events need ID property
    if (await _idempotencyService.IsProcessedAsync(eventId, idempotentHandler.HandlerName))
    {
        _logger.LogWarning("Domain event {EventId} already processed by {Handler}", 
            eventId, idempotentHandler.HandlerName);
        continue;
    }

    await mediator.Publish(domainEvent, ct);
    await _idempotencyService.MarkProcessedAsync(eventId, idempotentHandler.HandlerName);
}
```

## Conclusion

The Domain Events implementation follows DDD principles correctly but has a **CRITICAL architectural flaw**: events are dispatched INSIDE the database transaction AFTER SaveChanges. This creates a risk of data inconsistency if domain event handlers fail.

**Recommended Actions**:
1. 🔥 **URGENT**: Move domain event dispatching AFTER transaction commit (in TransactionBehavior)
2. 🔥 **HIGH**: Add event ordering to ensure deterministic handler execution
3. 🟡 **MEDIUM**: Pass aggregate root to handlers instead of IDs to avoid re-querying
4. 🟡 **MEDIUM**: Add retry policy for domain event handlers
5. 🟢 **LOW**: Add domain event store for audit trail

**Overall Risk Level**: 🔴 HIGH - Critical architectural issue with transaction boundaries

The pattern is conceptually correct, but the timing of event dispatch relative to transaction commit needs immediate attention to prevent data consistency issues.
