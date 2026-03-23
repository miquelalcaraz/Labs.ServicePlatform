# Transactional Outbox Pattern

## Executive Summary

### Brief Description
The Transactional Outbox pattern ensures reliable publishing of integration events by storing them in the same database transaction as business data. Events are saved to an outbox table within the transaction, then published to the message broker after commit. This guarantees at-least-once delivery and prevents message loss even if the message broker is unavailable during the transaction.

### Technical Implementation

**Location**:
- Outbox Entity: `src/Blocks/Shared.Messaging/Outbox/OutboxMessage.cs`
- Outbox Service: `src/Blocks/Shared.Persistence.EF/Outbox/OutboxMessageService.cs`
- Event Bus Service: `src/Blocks/Shared.Persistence.EF/Outbox/EventBusOutboxService.cs`
- Transaction Behavior: `src/Blocks/Shared.Persistence.EF/Behaviors/TransactionBehavior.cs`

**Core Components**:

1. **OutboxMessage Entity**:
```csharp
public class OutboxMessage
{
    public Guid EventId { get; private set; }
    public string EventTypeName { get; private set; }
    public string Content { get; private set; } // JSON serialized event
    public EventStateEnum State { get; set; } // NotPublished, InProgress, Published, PublishedFailed
    public int TimesSent { get; set; }
    public DateTime CreationTime { get; private set; }
    public Guid TransactionId { get; private set; }

    public OutboxMessage(IntegrationEvent @event, Guid transactionId)
    {
        EventId = @event.Id;
        CreationTime = @event.CreationDate;
        EventTypeName = @event.GetType().FullName;
        Content = JsonSerializer.Serialize(@event, @event.GetType());
        State = EventStateEnum.NotPublished;
        TransactionId = transactionId;
    }
}
```

2. **IOutboxMessageService** (Storage):
```csharp
public interface IOutboxMessageService
{
    Task<IEnumerable<OutboxMessage>> RetrieveEventLogsPendingToPublishAsync(Guid transactionId);
    Task SaveEventAsync(IntegrationEvent @event, IDbContextTransaction transaction);
    Task MarkEventAsPublishedAsync(Guid eventId);
    Task MarkEventAsInProgressAsync(Guid eventId);
    Task MarkEventAsFailedAsync(Guid eventId);
}
```

3. **IEventBusOutboxService** (Coordination):
```csharp
public interface IEventBusOutboxService
{
    Task AddAndSaveEventAsync(IntegrationEvent evt);
    Task PublishEventsThroughEventBusAsync(Guid transactionId);
}
```

4. **OutboxMessageService Implementation**:
```csharp
public class OutboxMessageService<TContext> : IOutboxMessageService
{
    public async Task SaveEventAsync(IntegrationEvent @event, IDbContextTransaction transaction)
    {
        if (transaction == null) throw new ArgumentNullException(nameof(transaction));

        var eventLogEntry = new OutboxMessage(@event, transaction.TransactionId);

        _context.Database.UseTransaction(transaction.GetDbTransaction());
        _context.Set<OutboxMessage>().Add(eventLogEntry);
        await _context.SaveChangesAsync();
    }

    public async Task<IEnumerable<OutboxMessage>> RetrieveEventLogsPendingToPublishAsync(Guid transactionId)
    {
        var events = await _context.Set<OutboxMessage>()
            .Where(e => e.TransactionId == transactionId && e.State == EventStateEnum.NotPublished)
            .ToListAsync();

        return events.OrderBy(o => o.CreationTime)
            .Select(e =>
            {
                var eventType = _eventTypes.FirstOrDefault(t => t.FullName == e.EventTypeName);
                if (eventType == null)
                    throw new InvalidOperationException($"Event type '{e.EventTypeName}' is not registered");

                return e.DeserializeJsonContent(eventType);
            })
            .ToList();
    }

    private async Task UpdateEventStatus(Guid eventId, EventStateEnum status)
    {
        var eventLogEntry = await _context.Set<OutboxMessage>()
            .SingleAsync(ie => ie.EventId == eventId);

        eventLogEntry.State = status;

        if (status == EventStateEnum.InProgress)
            eventLogEntry.TimesSent++;

        await _context.SaveChangesAsync();
    }
}
```

5. **EventBusOutboxService Implementation**:
```csharp
public class EventBusOutboxService<TContext> : IEventBusOutboxService
{
    public async Task AddAndSaveEventAsync(IntegrationEvent evt)
    {
        _logger.LogInformation("Saving integration event {EventId} to outbox", evt.Id);

        var transaction = _dbContext.GetCurrentTransaction();
        if (transaction == null)
            throw new InvalidOperationException("No active transaction");

        await _eventLogService.SaveEventAsync(evt, transaction);
    }

    public async Task PublishEventsThroughEventBusAsync(Guid transactionId)
    {
        var pendingEvents = await _eventLogService.RetrieveEventLogsPendingToPublishAsync(transactionId);

        foreach (var evt in pendingEvents)
        {
            _logger.LogInformation("Publishing integration event: {EventId}", evt.EventId);

            try
            {
                await _eventLogService.MarkEventAsInProgressAsync(evt.EventId);
                await _eventBus.PublishAsync(evt.IntegrationEvent);
                await _eventLogService.MarkEventAsPublishedAsync(evt.EventId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error publishing integration event: {EventId}", evt.EventId);
                await _eventLogService.MarkEventAsFailedAsync(evt.EventId);
            }
        }
    }
}
```

6. **TransactionBehavior Integration**:
```csharp
public class TransactionBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
{
    public async Task<TResponse> Handle(...)
    {
        await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await _dbContext.BeginTransactionAsync();
            
            response = await next(cancellationToken); // Command handler executes
            
            await _dbContext.CommitTransactionAsync(transaction); // Commit business data + outbox
            
            transactionId = transaction.TransactionId;
        });

        // Publish integration events AFTER successful commit
        if (_integrationEventService != null)
        {
            await _integrationEventService.PublishEventsThroughEventBusAsync(transactionId);
        }

        return response;
    }
}
```

7. **Example Usage**:
```csharp
// Domain Event Handler
public class OrganizationCreatedDomainEventHandler : IDomainEventHandler<OrganizationCreatedDomainEvent>
{
    public async Task Handle(OrganizationCreatedDomainEvent notification, CancellationToken ct)
    {
        var organization = await _repository.GetAsync(notification.OrganizationId);

        // Create integration event
        var integrationEvent = new OrganizationCreatedIntegrationEvent(
            organization.TenantId,
            organization.Id,
            organization.Name,
            organization.LegalName);

        // Save to outbox (within same transaction)
        await _outboxService.AddAndSaveEventAsync(integrationEvent);
    }
}
```

### Critical Points Detected

1. **✅ Transactional Consistency**:
   - Events saved in same transaction as business data
   - Atomic: either both business data and outbox entry are saved, or neither
   - **Benefit**: Guarantees no message loss

2. **✅ Events Published After Commit**:
   - `PublishEventsThroughEventBusAsync()` called AFTER `CommitTransactionAsync()`
   - Business data committed before attempting to publish
   - **Benefit**: Even if message broker fails, events are in outbox for retry

3. **⚠️ No Background Processor**:
   - Events only published immediately after transaction
   - No background job to retry failed events (State = PublishedFailed)
   - **Risk**: HIGH - Failed events never republished
   - **Impact**: CRITICAL - Messages lost if broker temporarily unavailable

4. **⚠️ Event State Transitions Not Atomic**:
   - Status updates (NotPublished → InProgress → Published) are separate transactions
   - Possible race condition if multiple publishers
   - **Risk**: Medium - Events could be published multiple times
   - **Impact**: Medium - Requires idempotent consumers

5. **⚠️ TimesSent Incremented on InProgress**:
   - `TimesSent++` when marked as InProgress, not on actual publish success
   - If publish fails between InProgress and Failed states, count is inaccurate
   - **Risk**: Low - Informational field only
   - **Impact**: Low - Metrics/monitoring affected

6. **⚠️ Single Transaction Query**:
   - `RetrieveEventLogsPendingToPublishAsync(transactionId)` only retrieves events for ONE transaction
   - Old events from previous failed transactions never republished
   - **Risk**: HIGH - Orphaned events if publisher crashes
   - **Impact**: CRITICAL - Message loss

7. **⚠️ Event Types Must Be Registered**:
   - `OutboxMessageOptions.EventTypes` must contain all event types
   - Runtime exception if unregistered event type encountered
   - **Risk**: Medium - Easy to forget when adding new events
   - **Impact**: Medium - Publish failure with unclear error

8. **✅ Serialization Preserves Full Type Name**:
   - Uses `@event.GetType().FullName` and `@event.GetType()` for serialization
   - Correct polymorphic deserialization
   - **Benefit**: Supports event inheritance

9. **⚠️ No Partitioning or Indexing Strategy**:
   - No index on `(State, CreationTime)` for efficient queries
   - No partitioning by date
   - **Risk**: Medium - Performance degradation as outbox grows
   - **Impact**: Medium - Slow queries for large outbox tables

10. **✅ OpenTelemetry Integration**:
    - Activities for retrieve, update, publish operations
    - Metrics for batch size, event state changes, operation duration
    - Good observability

### Possible Improvements

1. **🔥 CRITICAL: Add Background Outbox Processor**:
```csharp
public class OutboxProcessorBackgroundService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private const int BatchSize = 100;
    private const int MaxRetries = 5;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var context = scope.ServiceProvider.GetRequiredService<DbContext>();
                var eventBus = scope.ServiceProvider.GetRequiredService<IEventBus>();

                // Find failed or stuck events
                var pendingEvents = await context.Set<OutboxMessage>()
                    .Where(e => (e.State == EventStateEnum.PublishedFailed && e.TimesSent < MaxRetries) ||
                                (e.State == EventStateEnum.InProgress && e.CreationTime < DateTime.UtcNow.AddMinutes(-5)))
                    .OrderBy(e => e.CreationTime)
                    .Take(BatchSize)
                    .ToListAsync(stoppingToken);

                foreach (var outboxMessage in pendingEvents)
                {
                    try
                    {
                        var eventType = GetEventType(outboxMessage.EventTypeName);
                        var integrationEvent = outboxMessage.DeserializeJsonContent(eventType);

                        outboxMessage.State = EventStateEnum.InProgress;
                        outboxMessage.TimesSent++;
                        await context.SaveChangesAsync(stoppingToken);

                        await eventBus.PublishAsync(integrationEvent.IntegrationEvent);

                        outboxMessage.State = EventStateEnum.Published;
                        await context.SaveChangesAsync(stoppingToken);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to publish outbox event {EventId}", outboxMessage.EventId);
                        
                        outboxMessage.State = EventStateEnum.PublishedFailed;
                        await context.SaveChangesAsync(stoppingToken);
                    }
                }

                // Wait before next batch
                await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in outbox processor");
                await Task.Delay(TimeSpan.FromSeconds(60), stoppingToken);
            }
        }
    }
}
```

2. **Add Atomic State Updates with Optimistic Locking**:
```csharp
public class OutboxMessage
{
    public Guid EventId { get; set; }
    // ... other properties
    public int Version { get; set; } // For optimistic concurrency
}

// Configuration
builder.Property(e => e.Version).IsConcurrencyToken();

// In UpdateEventStatus:
private async Task UpdateEventStatus(Guid eventId, EventStateEnum status)
{
    var maxRetries = 3;
    for (int i = 0; i < maxRetries; i++)
    {
        try
        {
            var eventLogEntry = await _context.Set<OutboxMessage>()
                .SingleAsync(ie => ie.EventId == eventId);

            eventLogEntry.State = status;
            if (status == EventStateEnum.InProgress)
                eventLogEntry.TimesSent++;

            await _context.SaveChangesAsync();
            return;
        }
        catch (DbUpdateConcurrencyException)
        {
            if (i == maxRetries - 1) throw;
            // Retry on concurrency conflict
        }
    }
}
```

3. **Add Indexing for Efficient Queries**:
```csharp
// In OutboxMessageConfiguration
builder.HasIndex(e => new { e.State, e.CreationTime });
builder.HasIndex(e => new { e.TransactionId, e.State });
builder.HasIndex(e => e.EventTypeName); // For filtering by event type
```

4. **Add Outbox Cleanup Job**:
```csharp
public class OutboxCleanupBackgroundService : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var context = scope.ServiceProvider.GetRequiredService<DbContext>();

                // Delete published events older than 30 days
                var deleted = await context.Set<OutboxMessage>()
                    .Where(e => e.State == EventStateEnum.Published && 
                                e.CreationTime < DateTime.UtcNow.AddDays(-30))
                    .ExecuteDeleteAsync(stoppingToken);

                _logger.LogInformation("Cleaned up {Count} published outbox messages", deleted);

                // Archive failed events older than 90 days
                var archiveDate = DateTime.UtcNow.AddDays(-90);
                // Move to separate archive table or dead-letter queue
                
                await Task.Delay(TimeSpan.FromHours(24), stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in outbox cleanup");
                await Task.Delay(TimeSpan.FromHours(1), stoppingToken);
            }
        }
    }
}
```

5. **Add Circuit Breaker for Event Bus**:
```csharp
public class ResilientEventBusOutboxService : IEventBusOutboxService
{
    private readonly IEventBusOutboxService _inner;
    private readonly IAsyncPolicy _circuitBreaker;

    public ResilientEventBusOutboxService(IEventBusOutboxService inner)
    {
        _inner = inner;
        
        _circuitBreaker = Policy
            .Handle<Exception>()
            .CircuitBreakerAsync(
                handledEventsAllowedBeforeBreaking: 5,
                durationOfBreak: TimeSpan.FromMinutes(1),
                onBreak: (ex, duration) =>
                {
                    _logger.LogWarning("Circuit breaker opened for {Duration}", duration);
                },
                onReset: () =>
                {
                    _logger.LogInformation("Circuit breaker reset");
                });
    }

    public async Task PublishEventsThroughEventBusAsync(Guid transactionId)
    {
        await _circuitBreaker.ExecuteAsync(async () =>
        {
            await _inner.PublishEventsThroughEventBusAsync(transactionId);
        });
    }
}
```

6. **Add Event Type Registry with Auto-Discovery**:
```csharp
public class OutboxMessageOptions
{
    private readonly List<Type> _eventTypes = new();

    public IReadOnlyCollection<Type> EventTypes => _eventTypes.AsReadOnly();

    // Manually register
    public OutboxMessageOptions RegisterEventType<T>() where T : IntegrationEvent
    {
        _eventTypes.Add(typeof(T));
        return this;
    }

    // Auto-discover from assembly
    public OutboxMessageOptions RegisterFromAssembly(Assembly assembly)
    {
        var types = assembly.GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract && typeof(IntegrationEvent).IsAssignableFrom(t));

        _eventTypes.AddRange(types);
        return this;
    }

    // Auto-discover from current AppDomain
    public OutboxMessageOptions RegisterFromAllAssemblies()
    {
        var assemblies = AppDomain.CurrentDomain.GetAssemblies();
        foreach (var assembly in assemblies)
        {
            RegisterFromAssembly(assembly);
        }
        return this;
    }
}

// Usage in Startup:
services.Configure<OutboxMessageOptions>(options =>
{
    options.RegisterFromAssembly(typeof(OrganizationCreatedIntegrationEvent).Assembly);
});
```

7. **Add Partitioning by Date**:
```csharp
public class OutboxMessage
{
    public Guid EventId { get; set; }
    // ... other properties
    public int PartitionKey => CreationTime.Year * 10000 + CreationTime.Month * 100 + CreationTime.Day; // YYYYMMDD
}

// Configuration
builder.HasKey(e => new { e.EventId, e.PartitionKey });
builder.HasIndex(e => new { e.PartitionKey, e.State, e.CreationTime });

// Query with partition
var events = await context.Set<OutboxMessage>()
    .Where(e => e.PartitionKey >= startPartition && 
                e.PartitionKey <= endPartition &&
                e.State == EventStateEnum.NotPublished)
    .ToListAsync();
```

8. **Add Batch Publishing**:
```csharp
public async Task PublishEventsThroughEventBusAsync(Guid transactionId)
{
    var pendingEvents = await _eventLogService.RetrieveEventLogsPendingToPublishAsync(transactionId);
    
    if (pendingEvents.Count == 0) return;

    // Batch publish if event bus supports it
    try
    {
        var events = pendingEvents.Select(e => e.IntegrationEvent).ToList();
        await _eventBus.PublishBatchAsync(events);

        // Mark all as published
        foreach (var evt in pendingEvents)
        {
            await _eventLogService.MarkEventAsPublishedAsync(evt.EventId);
        }
    }
    catch
    {
        // Fall back to individual publishing
        foreach (var evt in pendingEvents)
        {
            // ... existing individual publish logic
        }
    }
}
```

## Conclusion

The Transactional Outbox implementation correctly ensures atomicity between business data and event storage. Events are published AFTER transaction commit, preventing message loss. However, there is a **CRITICAL missing component**: **no background processor to retry failed events**.

**Recommended Actions**:
1. 🔥 **CRITICAL**: Implement background outbox processor for failed/stuck events
2. 🔥 **HIGH**: Add cleanup job to prevent outbox table bloat
3. 🔥 **HIGH**: Add indexes on (State, CreationTime) for efficient queries
4. 🟡 **MEDIUM**: Add optimistic locking for state updates
5. 🟡 **MEDIUM**: Add circuit breaker pattern for event bus resilience
6. 🟢 **LOW**: Add event type auto-discovery
7. 🟢 **LOW**: Consider partitioning for large-scale deployments

**Overall Risk Level**: 🔴 HIGH - Failed events never republished

The pattern is architecturally correct, but the lack of a background processor means that any transient failures in the message broker result in permanent message loss. This defeats the purpose of the outbox pattern.
