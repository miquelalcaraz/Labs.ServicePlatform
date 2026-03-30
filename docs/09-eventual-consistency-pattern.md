# Eventual Consistency Pattern

## Executive Summary

### Brief Description
Eventual Consistency is a consistency model where data changes are not immediately visible across all services, but eventually become consistent over time through asynchronous message propagation. The implementation uses Domain Events for intra-bounded-context consistency and Integration Events (via Transactional Outbox) for inter-bounded-context consistency.

### Technical Implementation

**Location**: Distributed across multiple components:
- Domain Events: `src/Qlt.Platform/Qlt.Platform.Persistence.EntityFramework/Extensions/DomainEventsDispatcherExtension.cs`
- Outbox: `src/Qlt.Platform/Qlt.Platform.Persistence.EntityFramework/Outbox/`
- Inbox: `src/Qlt.Platform/Qlt.Platform.Persistence.EntityFramework/Inbox/`
- Event Bus: `src/Qlt.Platform/Qlt.Platform.EventBus/`

**Core Components**:

1. **Consistency Boundaries**:
```
┌─────────────────────────────────────────┐
│  Bounded Context (Accounts)             │
│  ┌────────────────────────────────────┐ │
│  │ Strong Consistency                 │ │
│  │ (Single Transaction)               │ │
│  │                                    │ │
│  │ Create Organization                │ │
│  │   ↓                                │ │
│  │ Save to DB                         │ │
│  │   ↓                                │ │
│  │ Dispatch Domain Events             │ │
│  │   ↓                                │ │
│  │ Save Integration Event to Outbox   │ │
│  │   ↓                                │ │
│  │ COMMIT TRANSACTION                 │ │
│  └────────────────────────────────────┘ │
│                                          │
│  Publish Integration Event (async)      │
│           ↓                              │
└───────────┼──────────────────────────────┘
            │ Message Broker (RabbitMQ)
            ↓
┌───────────┼──────────────────────────────┐
│  Bounded Context (Identity)              │
│  ┌────────────────────────────────────┐ │
│  │ Eventual Consistency               │ │
│  │ (Separate Transaction)             │ │
│  │                                    │ │
│  │ Receive Integration Event          │ │
│  │   ↓                                │ │
│  │ Check Inbox (duplicate?)           │ │
│  │   ↓                                │ │
│  │ Process Event                      │ │
│  │   ↓                                │ │
│  │ Mark in Inbox                      │ │
│  │   ↓                                │ │
│  │ COMMIT TRANSACTION                 │ │
│  └────────────────────────────────────┘ │
└──────────────────────────────────────────┘
```

2. **Domain Event Flow (Strong Consistency)**:
```csharp
// Within a single bounded context, same transaction
public class Organization : Entity, IAggregateRoot
{
    public static Organization Create(string name, ...)
    {
        var organization = new Organization { Name = name };
        
        // Raise domain event (in-memory)
        organization.AddDomainEvent(new OrganizationCreatedDomainEvent(
            organization.Id,
            organization.Name));

        return organization;
    }
}

// Command Handler
public async Task<Result<bool>> Handle(CreateOrganizationCommand request, ...)
{
    var organization = Organization.Create(request.Name, ...);
    
    await _repository.AddAsync(organization);
    
    // Saves organization AND dispatches domain events in SAME transaction
    await _unitOfWork.SaveEntitiesAsync(cancellationToken);

    return Result.Success(true);
}

// Domain Event Handler (SAME transaction)
public async Task Handle(OrganizationCreatedDomainEvent notification, ...)
{
    var organization = await _repository.GetAsync(notification.OrganizationId);

    // Create integration event and save to OUTBOX (SAME transaction)
    var integrationEvent = new OrganizationCreatedIntegrationEvent(...);
    await _outboxService.AddAndSaveEventAsync(integrationEvent);
}
```

3. **Integration Event Flow (Eventual Consistency)**:
```csharp
// After transaction commit, publish from outbox
await strategy.ExecuteAsync(async () =>
{
    await using var transaction = await _dbContext.BeginTransactionAsync();
    
    response = await next(cancellationToken); // Executes handler + domain events
    
    await _dbContext.CommitTransactionAsync(transaction); // COMMIT
    
    transactionId = transaction.TransactionId;
});

// NOW publish to message broker (OUTSIDE transaction)
if (_integrationEventService != null)
{
    await _integrationEventService.PublishEventsThroughEventBusAsync(transactionId);
}
```

4. **Consumer Side (Eventual Consistency)**:
```csharp
// Integration event handler in another bounded context
public class OrganizationCreatedIntegrationEventHandler 
    : IIntegrationEventHandler<OrganizationCreatedIntegrationEvent>
{
    public async Task Handle(OrganizationCreatedIntegrationEvent @event)
    {
        var handlerName = GetType().Name;

        // Check inbox for idempotency
        if (await _inbox.IsProcessedAsync(@event.Id, handlerName))
        {
            _logger.LogInformation("Event {EventId} already processed", @event.Id);
            return; // Skip duplicate
        }

        // Start NEW transaction (separate from producer)
        using var transaction = await _dbContext.Database.BeginTransactionAsync();

        try
        {
            // Process event (create user, update read model, etc.)
            await ProcessEventAsync(@event);

            // Mark in inbox
            await _inbox.MarkAsProcessedAsync(@event.Id, handlerName);

            // Commit consumer transaction
            await transaction.CommitAsync();
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            throw; // Message will be retried
        }
    }
}
```

5. **Consistency Guarantees**:

| Scope | Consistency Model | Mechanism | Guarantee |
|-------|------------------|-----------|-----------|
| Within Aggregate | Strong | Single transaction | ACID |
| Within Bounded Context | Strong | Domain Events in same transaction | ACID |
| Across Bounded Contexts | Eventual | Integration Events via Outbox | At-least-once delivery |
| Message Delivery | At-least-once | Outbox + Inbox | Idempotent consumers |

### Critical Points Detected

1. **✅ Clear Consistency Boundaries**:
   - Strong consistency WITHIN bounded context (domain events in transaction)
   - Eventual consistency ACROSS bounded contexts (integration events)
   - **Benefit**: Correct DDD bounded context implementation

2. **⚠️ Domain Events Inside Transaction**:
   - Domain events dispatched AFTER `SaveChanges()` but BEFORE commit
   - Domain event handlers execute in same transaction
   - **Risk**: HIGH - Handler failure causes rollback (see Domain Events doc)
   - **Impact**: CRITICAL - Can cause inconsistent state

3. **✅ Integration Events After Commit**:
   - Integration events published AFTER transaction commit
   - Even if message broker fails, events are in outbox
   - **Benefit**: Guarantees no data loss

4. **🔴 No Outbox Processor**:
   - Failed integration events never republished
   - No background job to retry failed outbox messages
   - **Risk**: CRITICAL - Eventual consistency never achieved if broker fails
   - **Impact**: CRITICAL - Data inconsistency across services

5. **⚠️ Manual Inbox Implementation**:
   - Handlers must manually check inbox and mark as processed
   - No automatic enforcement
   - **Risk**: HIGH - Handlers may forget, causing duplicate processing
   - **Impact**: HIGH - Data inconsistency in consumer services

6. **⚠️ No Compensation Logic**:
   - No saga pattern or compensating transactions
   - If downstream service fails, no automatic rollback
   - **Risk**: HIGH - Partial state changes across services
   - **Impact**: HIGH - Manual intervention required

7. **⚠️ No Event Versioning Strategy**:
   - Integration events have no version field
   - Schema changes break consumers
   - **Risk**: Medium - Deployment coordination required
   - **Impact**: Medium - Breaking changes require simultaneous deployment

8. **✅ Transactional Outbox**:
   - Integration events saved in same transaction as business data
   - Atomic: either both saved or neither
   - **Benefit**: Reliable message publishing

9. **⚠️ No Read Model Projection**:
   - No dedicated read models or materialized views
   - Queries hit write database directly
   - **Risk**: Low - CQRS not fully implemented
   - **Impact**: Medium - Performance limitation for complex queries

10. **⚠️ No Dead Letter Queue**:
    - Failed events marked as `PublishedFailed` but no separate queue
    - No mechanism to inspect and manually reprocess failed events
    - **Risk**: Medium - Lost visibility into failures
    - **Impact**: Medium - Difficult to diagnose consistency issues

### Possible Improvements

1. **🔥 CRITICAL: Implement Outbox Background Processor**:
```csharp
public class OutboxProcessorBackgroundService : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var context = scope.ServiceProvider.GetRequiredService<DbContext>();
                var eventBus = scope.ServiceProvider.GetRequiredService<IEventBus>();

                // Find failed or stuck events (with exponential backoff)
                var pendingEvents = await context.Set<OutboxMessage>()
                    .Where(e => 
                        // Failed events with retries remaining
                        (e.State == EventStateEnum.PublishedFailed && e.TimesSent < 10) ||
                        // Stuck in-progress events (>5 minutes)
                        (e.State == EventStateEnum.InProgress && 
                         e.CreationTime < DateTime.UtcNow.AddMinutes(-5)))
                    .OrderBy(e => e.CreationTime)
                    .Take(100)
                    .ToListAsync(stoppingToken);

                foreach (var outboxMessage in pendingEvents)
                {
                    // Calculate exponential backoff
                    var delayMs = Math.Pow(2, outboxMessage.TimesSent) * 1000;
                    if (DateTime.UtcNow < outboxMessage.CreationTime.AddMilliseconds(delayMs))
                        continue; // Too soon to retry

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
                        _logger.LogError(ex, "Failed to republish outbox event {EventId}", 
                            outboxMessage.EventId);
                        
                        outboxMessage.State = EventStateEnum.PublishedFailed;
                        await context.SaveChangesAsync(stoppingToken);
                    }
                }

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

2. **🔥 HIGH: Implement Saga Pattern for Compensating Transactions**:
```csharp
public interface ISaga<TData>
{
    Task<SagaResult> ExecuteAsync(TData data);
    Task CompensateAsync(TData data, SagaStep failedStep);
}

public class CreateOrganizationSaga : ISaga<CreateOrganizationData>
{
    public async Task<SagaResult> ExecuteAsync(CreateOrganizationData data)
    {
        var executedSteps = new List<SagaStep>();

        try
        {
            // Step 1: Create organization in Accounts
            executedSteps.Add(SagaStep.CreateOrganization);
            await CreateOrganizationAsync(data);

            // Step 2: Create user in Identity
            executedSteps.Add(SagaStep.CreateUser);
            await CreateUserAsync(data);

            // Step 3: Setup tenant in Multi-tenancy
            executedSteps.Add(SagaStep.SetupTenant);
            await SetupTenantAsync(data);

            return SagaResult.Success();
        }
        catch (Exception ex)
        {
            // Compensate in reverse order
            await CompensateAsync(data, executedSteps.Last());
            return SagaResult.Failure(ex);
        }
    }

    public async Task CompensateAsync(CreateOrganizationData data, SagaStep failedStep)
    {
        _logger.LogWarning("Compensating saga after failure at step {Step}", failedStep);

        // Compensate in reverse order
        if (failedStep >= SagaStep.SetupTenant)
            await DeleteTenantAsync(data.TenantId);

        if (failedStep >= SagaStep.CreateUser)
            await DeleteUserAsync(data.UserId);

        if (failedStep >= SagaStep.CreateOrganization)
            await DeleteOrganizationAsync(data.OrganizationId);
    }
}
```

3. **Add Event Versioning**:
```csharp
public record IntegrationEvent
{
    public Guid Id { get; init; }
    public DateTime CreationDate { get; init; }
    public int Version { get; init; } = 1; // NEW
    public string EventType { get; init; } // NEW

    public IntegrationEvent()
    {
        Id = Guid.NewGuid();
        CreationDate = DateTime.UtcNow;
        EventType = GetType().Name;
    }
}

// Versioned events
public record OrganizationCreatedIntegrationEvent_V1 : IntegrationEvent
{
    public Guid OrganizationId { get; init; }
    public string Name { get; init; }
}

public record OrganizationCreatedIntegrationEvent_V2 : IntegrationEvent
{
    public Guid OrganizationId { get; init; }
    public string Name { get; init; }
    public string LegalName { get; init; } // NEW field
    public string? TaxIdNumber { get; init; } // NEW field

    public OrganizationCreatedIntegrationEvent_V2() => Version = 2;
}

// Handler supports multiple versions
public class OrganizationCreatedEventHandler : 
    IIntegrationEventHandler<OrganizationCreatedIntegrationEvent_V1>,
    IIntegrationEventHandler<OrganizationCreatedIntegrationEvent_V2>
{
    public async Task Handle(OrganizationCreatedIntegrationEvent_V1 @event)
    {
        await ProcessV1(@event);
    }

    public async Task Handle(OrganizationCreatedIntegrationEvent_V2 @event)
    {
        await ProcessV2(@event);
    }
}
```

4. **Add Read Model Projections**:
```csharp
// Read model (denormalized view)
public class OrganizationReadModel
{
    public Guid Id { get; set; }
    public string Name { get; set; }
    public string LegalName { get; set; }
    public string OwnerName { get; set; } // From User aggregate
    public string Country { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime LastUpdated { get; set; }
}

// Projection handler
public class OrganizationCreatedProjection : IIntegrationEventHandler<OrganizationCreatedIntegrationEvent>
{
    public async Task Handle(OrganizationCreatedIntegrationEvent @event)
    {
        // Update read model
        var readModel = new OrganizationReadModel
        {
            Id = @event.OrganizationId,
            Name = @event.Name,
            LegalName = @event.LegalName,
            Country = @event.CountryCode,
            CreatedAt = @event.CreationDate,
            LastUpdated = DateTime.UtcNow
        };

        await _readModelRepository.UpsertAsync(readModel);
    }
}

// Query uses read model
public class GetOrganizationQueryHandler : IQueryHandler<GetOrganizationQuery, OrganizationViewModel>
{
    public async Task<Result<OrganizationViewModel>> Handle(GetOrganizationQuery request, ...)
    {
        // Query from read model (fast, denormalized)
        var readModel = await _readModelRepository.GetByIdAsync(request.Id);
        
        return Result.Success(new OrganizationViewModel
        {
            Id = readModel.Id,
            Name = readModel.Name,
            Owner = readModel.OwnerName, // Already joined!
            Country = readModel.Country
        });
    }
}
```

5. **Add Dead Letter Queue**:
```csharp
public class DeadLetterQueue
{
    public Guid Id { get; set; }
    public Guid OriginalEventId { get; set; }
    public string EventType { get; set; }
    public string EventData { get; set; }
    public string ErrorMessage { get; set; }
    public string StackTrace { get; set; }
    public int RetryCount { get; set; }
    public DateTime FailedAt { get; set; }
}

// In outbox processor:
if (outboxMessage.TimesSent >= 10) // Max retries exceeded
{
    // Move to dead letter queue
    await _deadLetterQueue.AddAsync(new DeadLetterQueue
    {
        OriginalEventId = outboxMessage.EventId,
        EventType = outboxMessage.EventTypeName,
        EventData = outboxMessage.Content,
        ErrorMessage = "Max retries exceeded",
        RetryCount = outboxMessage.TimesSent,
        FailedAt = DateTime.UtcNow
    });

    // Remove from outbox or mark as permanently failed
    outboxMessage.State = EventStateEnum.MovedToDeadLetter;
    await context.SaveChangesAsync();
}
```

6. **Add Consistency Monitoring Dashboard**:
```csharp
public class ConsistencyMonitor
{
    public async Task<ConsistencyReport> GenerateReportAsync()
    {
        return new ConsistencyReport
        {
            OutboxStatistics = new OutboxStats
            {
                TotalPending = await CountPendingOutbox(),
                TotalFailed = await CountFailedOutbox(),
                OldestPendingAge = await GetOldestPendingAge(),
                FailureRate = await CalculateFailureRate()
            },
            InboxStatistics = new InboxStats
            {
                TotalProcessed = await CountProcessedInbox(),
                DuplicatesDetected = await CountDuplicates(),
                ProcessingLatency = await CalculateAverageLatency()
            },
            DeadLetterStatistics = new DeadLetterStats
            {
                TotalMessages = await CountDeadLetters(),
                EventTypes = await GetDeadLetterEventTypes()
            }
        };
    }
}

// API endpoint
[HttpGet("api/monitoring/consistency")]
public async Task<IActionResult> GetConsistencyReport()
{
    var report = await _monitor.GenerateReportAsync();
    return Ok(report);
}
```

7. **Add Event Sourcing for Critical Aggregates**:
```csharp
public class OrganizationEventSourced : Entity
{
    private readonly List<IDomainEvent> _uncommittedEvents = new();

    // No properties, just events
    private Guid _id;
    private string _name;
    private OrganizationStatus _status;

    // Apply events to rebuild state
    public void Apply(OrganizationCreatedEvent @event)
    {
        _id = @event.OrganizationId;
        _name = @event.Name;
        _status = OrganizationStatus.Active;
    }

    public void Apply(OrganizationNameChangedEvent @event)
    {
        _name = @event.NewName;
    }

    // Raise new events
    public void ChangeName(string newName)
    {
        if (_name == newName) return;

        var @event = new OrganizationNameChangedEvent(_id, _name, newName);
        ApplyAndRecord(@event);
    }

    private void ApplyAndRecord(IDomainEvent @event)
    {
        Apply((dynamic)@event);
        _uncommittedEvents.Add(@event);
    }

    // Load from event stream
    public static OrganizationEventSourced LoadFromHistory(IEnumerable<IDomainEvent> events)
    {
        var org = new OrganizationEventSourced();
        foreach (var @event in events)
        {
            org.Apply((dynamic)@event);
        }
        return org;
    }
}
```

## Conclusion

The Eventual Consistency implementation correctly separates strong consistency (within bounded context) from eventual consistency (across bounded contexts). The use of Transactional Outbox ensures reliable message publishing. However, there are **CRITICAL missing components** for achieving true eventual consistency:

**Strengths**:
- ✅ Clear consistency boundaries
- ✅ Transactional outbox for reliable publishing
- ✅ Integration events published after commit
- ✅ Idempotent consumer pattern (inbox)

**Critical Gaps**:
- 🔴 **No outbox background processor** - failed events never republished
- 🔴 **Domain events inside transaction** - handler failures cause rollback
- 🔴 **Manual inbox implementation** - inconsistent across handlers
- 🔴 **No compensation logic** - partial failures leave inconsistent state

**Recommended Actions**:
1. 🔥 **CRITICAL**: Implement outbox background processor with retry logic
2. 🔥 **CRITICAL**: Move domain event dispatch after transaction commit
3. 🔥 **CRITICAL**: Implement automatic inbox decorator for all handlers
4. 🔥 **HIGH**: Implement saga pattern for compensating transactions
5. 🔥 **HIGH**: Add dead letter queue for permanently failed events
6. 🟡 **MEDIUM**: Add event versioning strategy
7. 🟡 **MEDIUM**: Add consistency monitoring dashboard
8. 🟢 **LOW**: Add read model projections for CQRS
9. 🟢 **LOW**: Consider event sourcing for critical aggregates

**Overall Risk Level**: 🔴 CRITICAL - Eventual consistency cannot be achieved without outbox processor

The architecture has the right building blocks (outbox, inbox, integration events), but without a background processor to retry failed events, eventual consistency will never be achieved when the message broker is unavailable. This is the single most critical missing piece in the entire architecture.

**Key Insight**: The system can achieve strong consistency within bounded contexts, but cannot achieve eventual consistency across bounded contexts without the outbox processor. This defeats the purpose of the distributed architecture.
