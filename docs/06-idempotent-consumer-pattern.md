# Idempotent Consumer Pattern (Integration Event Inbox)

## Executive Summary

### Brief Description
The Idempotent Consumer pattern ensures that integration events from external bounded contexts are processed exactly once, even if delivered multiple times by the message broker. Implementation uses a database inbox table that tracks which events have been processed by which handlers, providing per-handler idempotency.

### Technical Implementation

**Location**:
- Interface: `src/Qlt.Platform/Qlt.Platform.Messaging.Abstractions/Inbox/IIntegrationEventInbox.cs`
- Entity: `src/Qlt.Platform/Qlt.Platform.Messaging/Inbox/IntegrationEventInbox.cs`
- Service: `src/Qlt.Platform/Qlt.Platform.Persistence.EntityFramework/Inbox/IntegrationEventInboxService.cs`

**Core Components**:

1. **IIntegrationEventInbox Interface**:
```csharp
public interface IIntegrationEventInbox
{
    Task<bool> IsProcessedAsync(Guid eventId, string handlerName);
    Task MarkAsProcessedAsync(Guid eventId, string handlerName, string? eventType = null);
}
```

2. **IntegrationEventInbox Entity**:
```csharp
public class IntegrationEventInbox
{
    public Guid EventId { get; set; } // From IntegrationEvent.Id
    public string HandlerName { get; set; } // Handler type name
    public DateTime ProcessedAt { get; set; }
    public string? EventType { get; set; } // Optional, for auditing
}

// Composite Primary Key (EventId, HandlerName)
```

3. **IntegrationEventInboxService Implementation**:
```csharp
public class IntegrationEventInboxService<TContext> : IIntegrationEventInbox 
    where TContext : DbContext
{
    public async Task<bool> IsProcessedAsync(Guid eventId, string handlerName)
    {
        using var activity = IdempotencyHelper.StartEventIdempotencyCheck(eventId, handlerName);

        var exists = await _context.Set<IntegrationEventInbox>()
            .AnyAsync(e => e.Id == eventId && e.HandlerName == handlerName);

        IdempotencyHelper.RecordCheckResult(activity, exists, elapsedMs);

        if (exists)
        {
            _logger.LogInformation("Event {EventId} already processed by {HandlerName}. Skipping duplicate.", 
                eventId, handlerName);
        }

        return exists;
    }

    public async Task MarkAsProcessedAsync(Guid eventId, string handlerName, string? eventType = null)
    {
        using var activity = IdempotencyHelper.StartMarkEventAsProcessed(eventId, handlerName);

        var processedEvent = new IntegrationEventInbox
        {
            EventId = eventId,
            HandlerName = handlerName,
            ProcessedAt = DateTime.UtcNow,
            EventType = eventType
        };

        _context.Set<IntegrationEventInbox>().Add(processedEvent);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Marked event {EventId} as processed by {HandlerName}", eventId, handlerName);
    }
}
```

4. **Database Configuration**:
```csharp
// Composite primary key
builder.HasKey(e => new { e.Id, e.HandlerName });

// Index for queries
builder.HasIndex(e => new { e.HandlerName, e.ProcessedAt });
```

5. **Expected Usage Pattern** (Manual in handlers):
```csharp
public class OrganizationCreatedIntegrationEventHandler : IIntegrationEventHandler<OrganizationCreatedIntegrationEvent>
{
    private readonly IIntegrationEventInbox _inbox;

    public async Task Handle(OrganizationCreatedIntegrationEvent @event)
    {
        var handlerName = GetType().Name; // "OrganizationCreatedIntegrationEventHandler"

        // Check if already processed
        if (await _inbox.IsProcessedAsync(@event.Id, handlerName))
        {
            _logger.LogInformation("Event {EventId} already processed, skipping", @event.Id);
            return;
        }

        try
        {
            // Process event
            await ProcessEventAsync(@event);

            // Mark as processed
            await _inbox.MarkAsProcessedAsync(@event.Id, handlerName, @event.GetType().Name);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process event {EventId}", @event.Id);
            throw; // Message will be retried
        }
    }
}
```

### Critical Points Detected

1. **⚠️ No Automatic Enforcement**:
   - Idempotency check is NOT automatic - handlers must manually call `IsProcessedAsync()`
   - Easy to forget, leading to duplicate processing
   - **Risk**: HIGH - Inconsistent implementation across handlers
   - **Impact**: CRITICAL - Some handlers may process duplicates

2. **⚠️ Mark Called in Handler (Not Transaction-Safe)**:
   - `MarkAsProcessedAsync()` called by handler after business logic
   - If transaction fails after marking, event marked but not processed
   - **Risk**: HIGH - Lost events (marked but not actually processed)
   - **Impact**: CRITICAL - Data inconsistency

3. **⚠️ No Unique Constraint**:
   - Primary key is composite (EventId, HandlerName)
   - No explicit UNIQUE constraint beyond primary key
   - Database may allow duplicates if key not properly enforced
   - **Risk**: Medium - Depends on database provider
   - **Impact**: Medium - Duplicate processing possible

4. **⚠️ No Transactional Wrapping**:
   - `SaveChangesAsync()` called directly in service
   - Not part of handler's transaction
   - Possible race condition: multiple consumers check simultaneously before any mark
   - **Risk**: Medium - Window between check and mark
   - **Impact**: Medium - Duplicate processing during race

5. **⚠️ No Cleanup Mechanism**:
   - Inbox table grows indefinitely
   - No TTL or cleanup job
   - **Risk**: Medium - Table bloat
   - **Impact**: Medium - Performance degradation over time

6. **✅ Per-Handler Idempotency**:
   - Composite key (EventId, HandlerName) allows same event to be processed by different handlers
   - Correct for pub/sub scenarios with multiple subscribers
   - **Benefit**: Flexibility for multiple consumers

7. **✅ OpenTelemetry Integration**:
   - Activities and metrics for idempotency checks
   - Good observability for duplicate detection
   - `IdempotencyHelper.RecordDuplicateEvent()` logs duplicates

8. **⚠️ HandlerName Hardcoded as String**:
   - Handler name stored as string (`typeof(Handler).Name`)
   - If handler class renamed, old inbox entries become orphaned
   - **Risk**: Low - Class renames are rare
   - **Impact**: Low - Historical data affected

### Possible Improvements

1. **🔥 CRITICAL: Add Automatic Inbox Behavior**:
```csharp
// Marker interface for handlers that need inbox
public interface IIdempotentIntegrationEventHandler<TEvent> : IIntegrationEventHandler<TEvent>
    where TEvent : IntegrationEvent
{
}

// Decorator pattern for automatic inbox handling
public class IdempotencyIntegrationEventDecorator<TEvent> : IIntegrationEventHandler<TEvent>
    where TEvent : IntegrationEvent
{
    private readonly IIntegrationEventHandler<TEvent> _inner;
    private readonly IIntegrationEventInbox _inbox;
    private readonly ILogger _logger;

    public async Task Handle(TEvent @event)
    {
        var handlerName = _inner.GetType().Name;

        // Automatic idempotency check
        if (await _inbox.IsProcessedAsync(@event.Id, handlerName))
        {
            _logger.LogInformation("Duplicate event {EventId} detected for {Handler}", 
                @event.Id, handlerName);
            return;
        }

        try
        {
            // Execute inner handler
            await _inner.Handle(@event);

            // Mark as processed AFTER successful execution
            await _inbox.MarkAsProcessedAsync(@event.Id, handlerName, typeof(TEvent).Name);
        }
        catch (Exception)
        {
            // Don't mark as processed if handler failed
            throw;
        }
    }
}

// Registration in DI
services.Decorate(typeof(IIntegrationEventHandler<>), typeof(IdempotencyIntegrationEventDecorator<>));
```

2. **🔥 HIGH: Integrate with Handler Transaction**:
```csharp
public class TransactionalIntegrationEventHandler<TEvent> : IIntegrationEventHandler<TEvent>
    where TEvent : IntegrationEvent
{
    private readonly IIntegrationEventHandler<TEvent> _inner;
    private readonly DbContext _context;
    private readonly IIntegrationEventInbox _inbox;

    public async Task Handle(TEvent @event)
    {
        var handlerName = _inner.GetType().Name;

        // Check inbox (outside transaction for performance)
        if (await _inbox.IsProcessedAsync(@event.Id, handlerName))
            return;

        // Start transaction
        await using var transaction = await _context.Database.BeginTransactionAsync();

        try
        {
            // Execute handler (business logic)
            await _inner.Handle(@event);

            // Mark as processed WITHIN same transaction
            _context.Set<IntegrationEventInbox>().Add(new IntegrationEventInbox
            {
                EventId = @event.Id,
                HandlerName = handlerName,
                ProcessedAt = DateTime.UtcNow,
                EventType = typeof(TEvent).Name
            });

            // Commit both business data and inbox entry atomically
            await _context.SaveChangesAsync();
            await transaction.CommitAsync();
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }
}
```

3. **Add Database-Enforced Unique Constraint**:
```csharp
// In IntegrationEventInboxConfiguration
builder.HasKey(e => new { e.Id, e.HandlerName });
builder.HasIndex(e => new { e.Id, e.HandlerName }).IsUnique(); // Explicit unique constraint

// Handle duplicate insert as expected behavior
try
{
    await _context.SaveChangesAsync();
}
catch (DbUpdateException ex) when (_context.IsUniqueConstraintViolation(ex))
{
    // Expected for duplicate events - inbox prevents reprocessing
    _logger.LogInformation("Event {EventId} already in inbox for {Handler} (database enforced)", 
        eventId, handlerName);
}
```

4. **Add Optimistic Locking for Check-Then-Mark**:
```csharp
public async Task<bool> TryMarkAsProcessingAsync(Guid eventId, string handlerName)
{
    try
    {
        // Atomic insert (if already exists, constraint violation)
        var entry = new IntegrationEventInbox
        {
            EventId = eventId,
            HandlerName = handlerName,
            ProcessedAt = DateTime.UtcNow
        };

        _context.Set<IntegrationEventInbox>().Add(entry);
        await _context.SaveChangesAsync();

        return true; // Successfully marked
    }
    catch (DbUpdateException ex) when (_context.IsUniqueConstraintViolation(ex))
    {
        return false; // Already processed
    }
}

// Usage:
public async Task Handle(TEvent @event)
{
    var handlerName = GetType().Name;

    // Atomic check-and-mark
    if (!await _inbox.TryMarkAsProcessingAsync(@event.Id, handlerName))
    {
        _logger.LogInformation("Event {EventId} already processed", @event.Id);
        return;
    }

    // Process event (if we get here, we're guaranteed to be the only processor)
    await ProcessEventAsync(@event);
}
```

5. **Add Cleanup Job with TTL**:
```csharp
public class InboxCleanupBackgroundService : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var context = scope.ServiceProvider.GetRequiredService<DbContext>();

                // Delete processed events older than 30 days
                var deleted = await context.Set<IntegrationEventInbox>()
                    .Where(e => e.ProcessedAt < DateTime.UtcNow.AddDays(-30))
                    .ExecuteDeleteAsync(stoppingToken);

                _logger.LogInformation("Cleaned up {Count} processed inbox entries", deleted);

                await Task.Delay(TimeSpan.FromHours(24), stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in inbox cleanup");
                await Task.Delay(TimeSpan.FromHours(1), stoppingToken);
            }
        }
    }
}
```

6. **Add Memory Cache for Recent Events**:
```csharp
public class CachedIntegrationEventInbox : IIntegrationEventInbox
{
    private readonly IIntegrationEventInbox _inner;
    private readonly IMemoryCache _cache;
    private const int CacheDurationMinutes = 5;

    public async Task<bool> IsProcessedAsync(Guid eventId, string handlerName)
    {
        var cacheKey = $"inbox:{eventId}:{handlerName}";

        // Check cache first
        if (_cache.TryGetValue(cacheKey, out _))
            return true;

        // Check database
        var isProcessed = await _inner.IsProcessedAsync(eventId, handlerName);

        // Cache positive results
        if (isProcessed)
        {
            _cache.Set(cacheKey, true, TimeSpan.FromMinutes(CacheDurationMinutes));
        }

        return isProcessed;
    }

    public async Task MarkAsProcessedAsync(Guid eventId, string handlerName, string? eventType = null)
    {
        await _inner.MarkAsProcessedAsync(eventId, handlerName, eventType);

        // Add to cache immediately
        var cacheKey = $"inbox:{eventId}:{handlerName}";
        _cache.Set(cacheKey, true, TimeSpan.FromMinutes(CacheDurationMinutes));
    }
}
```

7. **Add Handler Name Stability**:
```csharp
// Use custom attribute instead of class name
[HandlerName("OrganizationCreatedHandler.V1")]
public class OrganizationCreatedIntegrationEventHandler : IIntegrationEventHandler<OrganizationCreatedIntegrationEvent>
{
    public async Task Handle(OrganizationCreatedIntegrationEvent @event)
    {
        var handlerName = GetHandlerName(); // Gets from attribute
        // ...
    }
}

public static class HandlerExtensions
{
    public static string GetHandlerName(this IIntegrationEventHandler handler)
    {
        var attribute = handler.GetType().GetCustomAttribute<HandlerNameAttribute>();
        return attribute?.Name ?? handler.GetType().FullName;
    }
}
```

8. **Add Event Replay/Reprocessing Support**:
```csharp
public interface IIntegrationEventInbox
{
    Task<bool> IsProcessedAsync(Guid eventId, string handlerName);
    Task MarkAsProcessedAsync(Guid eventId, string handlerName, string? eventType = null);
    Task ResetProcessedStateAsync(Guid eventId, string handlerName); // NEW
    Task<IEnumerable<IntegrationEventInbox>> GetProcessedEventsAsync(string handlerName, DateTime since); // NEW
}

// For manual replay scenarios
public async Task ReplayEvents(DateTime since)
{
    var processedEvents = await _inbox.GetProcessedEventsAsync("MyHandler", since);

    foreach (var processed in processedEvents)
    {
        // Reset state
        await _inbox.ResetProcessedStateAsync(processed.Id, processed.HandlerName);

        // Event will be reprocessed on next delivery
    }
}
```

9. **Add Partitioning for Scale**:
```csharp
public class IntegrationEventInbox
{
    public Guid EventId { get; set; }
    public string HandlerName { get; set; }
    public DateTime ProcessedAt { get; set; }
    public string? EventType { get; set; }
    public int PartitionKey => ProcessedAt.Year * 100 + ProcessedAt.Month; // YYYYMM
}

// Configuration
builder.HasKey(e => new { e.Id, e.HandlerName, e.PartitionKey });
builder.HasIndex(e => new { e.PartitionKey, e.ProcessedAt });
```

## Conclusion

The Idempotent Consumer implementation has the right data structure (composite key for per-handler idempotency) but has **CRITICAL implementation gaps**:

1. **No automatic enforcement** - handlers must manually implement idempotency checks
2. **Mark happens outside business transaction** - creates race conditions and lost events
3. **No cleanup** - inbox table grows indefinitely

**Recommended Actions**:
1. 🔥 **CRITICAL**: Implement automatic inbox decorator to enforce idempotency for all handlers
2. 🔥 **CRITICAL**: Mark events as processed WITHIN handler transaction for atomicity
3. 🔥 **HIGH**: Add database-enforced unique constraint with proper exception handling
4. 🔥 **HIGH**: Add cleanup job with TTL
5. 🟡 **MEDIUM**: Add memory caching for recent events (performance)
6. 🟡 **MEDIUM**: Add atomic TryMarkAsProcessing method
7. 🟢 **LOW**: Add handler name stability mechanism

**Overall Risk Level**: 🔴 HIGH - Manual implementation leads to inconsistent idempotency

The pattern design is correct, but without automatic enforcement via decorators/behaviors, handlers will inevitably be implemented inconsistently, leading to duplicate processing. The lack of transactional wrapping also creates a window for race conditions.
