# Domain Events Pattern

## Executive Summary

### Brief Description
Domain Events represent state changes within a bounded context that other parts of the same bounded context need to react to. They enable loose coupling between aggregates while maintaining transactional consistency. Events are dispatched AFTER database SaveChanges but WITHIN the same transaction, ensuring all side effects are atomic.

### Technical Implementation

**Location**: 
- Interface: `src/Blocks/Shared.Messaging.Abstractions/DomainEvents/IDomainEvent.cs`
- Dispatcher: `src/Blocks/Shared.Persistence.EF/Extensions/DomainEventsDispatcherExtension.cs`
- Entity Base: `src/Blocks/Core.Domain/SeedWork/Entity.cs`
- Example Handler: `src/Services.Accounts/Application/DomainEventHandlers/OrganizationCreatedDomainEventHandler.cs`

**Core Components**:

1. **IDomainEvent Interface**:
```csharp
public interface IDomainEvent : INotification { }

public interface IDomainEventHandler<TEvent> : INotificationHandler<TEvent>
    where TEvent : IDomainEvent { }
```

2. **Entity Domain Event Collection**:
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

3. **Domain Event Dispatcher**:
```csharp
public static async Task DispatchDomainEventsAsync(this IMediator mediator, DbContext ctx, CancellationToken ct)
{
    // Collect events from tracked entities
    var domainEvents = ctx.ChangeTracker
        .Entries<Entity>()
        .Where(x => x.Entity.DomainEvents != null && x.Entity.DomainEvents.Any())
        .SelectMany(x => x.Entity.DomainEvents!)
        .ToList();

    // Clear events from entities
    domainEntities.ForEach(entity => entity.Entity.ClearDomainEvents());

    // Publish via MediatR
    foreach (var domainEvent in domainEvents)
    {
        ApplicationDiagnostics.DomainEventsPublished.Add(1,
            new KeyValuePair<string, object?>("event_type", eventType));
        await mediator.Publish(domainEvent, ct);
    }
}
```

4. **Dispatcher Invocation** (in QltDBContextBase):
```csharp
public async Task<bool> SaveEntitiesAsync(CancellationToken cancellationToken = default)
{
    if (_configuration.UseAuditLogs)
        await this.SaveAuditLogsAsync(cancellationToken);

    // Save changes first (INSERT/UPDATE entities)
    await base.SaveChangesAsync(cancellationToken);

    // Dispatch domain events AFTER SaveChanges but BEFORE transaction commit
    await _mediator.DispatchDomainEventsAsync(this, cancellationToken);

    return true;
}
```

5. **Example Usage**:
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

// Domain Event Handler
public class OrganizationCreatedDomainEventHandler : IDomainEventHandler<OrganizationCreatedDomainEvent>
{
    public async Task Handle(OrganizationCreatedDomainEvent notification, CancellationToken ct)
    {
        // Verify entity is tracked
        var organization = await _repository.GetAsync(notification.OrganizationId);

        if (organization != null)
        {
            // Publish integration event to outbox
            var integrationEvent = new OrganizationCreatedIntegrationEvent(...);
            await _outboxService.AddAndSaveEventAsync(integrationEvent);
        }
    }
}
```

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
