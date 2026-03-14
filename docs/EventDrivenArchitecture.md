# QltSystem - Event-Driven Architecture Components

This directory contains documentation for the core event-driven architecture components of QltSystem.

## Components Overview

### 1. [EventBus.Idempotency](EventBus.Idempotency.md)
**Pattern**: Idempotent Consumer  
**Purpose**: Prevents duplicate processing of **incoming integration events**

**Use when**: Your service **consumes** integration events and needs to handle duplicates gracefully.

**Key Features**:
- Tracks processed events by (EventId + HandlerName)
- Database-backed with automatic configuration
- Prevents duplicate event processing

**Example scenario**: Receiving `OrganizationCreatedEvent` from message broker - process once even if delivered multiple times.

---

### 2. [EventBus.TransactionalOutbox](EventBus.TransactionalOutbox.md)
**Pattern**: Transactional Outbox  
**Purpose**: Ensures reliable publishing of **outgoing integration events**

**Use when**: Your service **publishes** integration events and needs to guarantee delivery.

**Key Features**:
- Events saved in same transaction as business data
- Automatic publishing after successful commit
- Exactly-once delivery guarantee
- Auto-registers `IIntegrationEventService`
- Auto-registers `TransactionBehavior` via `AddTransactionalOutboxBehavior()`

**Example scenario**: Creating an account and publishing `AccountCreatedEvent` - event is published only if account creation succeeds.

---

### 3. [CommandIdempotency](CommandIdempotency.md)
**Pattern**: Idempotent Command  
**Purpose**: Prevents duplicate execution of **incoming commands**

**Use when**: Your service receives commands (HTTP API, messages) and needs to handle duplicate requests.

**Key Features**:
- Tracks processed commands by correlation/request ID
- Database-backed with automatic configuration
- Layered architecture (EF Core → Relational)
- Extensible to other persistence providers (Cosmos, MongoDB)

**Example scenario**: Receiving `CreateOrderCommand` via HTTP API - execute once even if client retries.

---

## Architecture Patterns

### Event-Driven Communication Flow

```
┌──────────────────┐                         ┌──────────────────┐
│  Service A       │                         │  Service B       │
│  (Publisher)     │                         │  (Consumer)      │
├──────────────────┤                         ├──────────────────┤
│                  │                         │                  │
│ 1. Command       │                         │                  │
│    ↓             │                         │                  │
│ 2. Domain Event  │                         │                  │
│    ↓             │                         │                  │
│ 3. Integration   │    Message Broker      │ 6. Integration   │
│    Event (Outbox)│ ─────RabbitMQ────────→ │    Event Handler │
│    ↓             │                         │    ↓             │
│ 4. Transaction   │                         │ 7. Idempotency   │
│    Commit        │                         │    Check         │
│    ↓             │                         │    ↓             │
│ 5. Publish       │                         │ 8. Process       │
│                  │                         │                  │
└──────────────────┘                         └──────────────────┘

      ↓                                               ↓
CommandIdempotency                          EventBus.Idempotency
(Prevents duplicate commands)               (Prevents duplicate event processing)

TransactionalOutbox
(Ensures event published after commit)
```

### Typical Service Configuration

#### Publisher Service (e.g., Accounts)
```csharp
using Core.Persistence.EF.Relational.Extensions;
using QltSystem.Core.EventBus.TransactionalOutbox.Extensions;

// DbContext
services.AddQltSystemDBContext<AccountContext>(...);

// Outgoing events (publish)
services.AddTransactionalOutbox<AccountContext>(options =>
{
    options.RegisterEventTypes(typeof(AccountEvents).Assembly);
});

// Incoming commands (HTTP API)
services.AddCommandIdempotency<AccountContext>();

// MediatR with transaction behavior
services.AddMediatR(cfg =>
{
    cfg.RegisterServicesFromAssemblyContaining<AssemblyReference>();
    cfg.AddTransactionalOutboxBehavior();  // Required for outbox
});
```

#### Consumer Service (e.g., Identity)
```csharp
using Core.Persistence.EF.Relational.Extensions;
using QltSystem.Core.EventBus.Idempotency.Extensions;

// DbContext
services.AddQltSystemDBContext<IdentityContext>(...);

// Incoming events (consume)
services.AddEventIdempotency<IdentityContext>();

// Event subscriptions
builder.AddRabbitMqEventBus("eventbus")
    .AddSubscription<OrganizationCreatedEvent, OrganizationCreatedHandler>();
```

#### Full Service (publishes and consumes)
```csharp
using Core.Persistence.EF.Relational.Extensions;
using QltSystem.Core.EventBus.TransactionalOutbox.Extensions;
using QltSystem.Core.EventBus.Idempotency.Extensions;

services.AddQltSystemDBContext<ServiceContext>(...);

// Publisher side
services.AddTransactionalOutbox<ServiceContext>(...);
services.AddCommandIdempotency<ServiceContext>();

// Consumer side
services.AddEventIdempotency<ServiceContext>();

// MediatR
services.AddMediatR(cfg =>
{
    cfg.RegisterServicesFromAssemblyContaining<AssemblyReference>();
    cfg.AddTransactionalOutboxBehavior();
});
```

---

## Component Comparison

| Component | Incoming/Outgoing | What it prevents | Database Table |
|-----------|-------------------|------------------|----------------|
| **EventBus.Idempotency** | Incoming Events | Duplicate event processing | `ProcessedIntegrationEvents` |
| **EventBus.TransactionalOutbox** | Outgoing Events | Lost events on failure | `IntegrationEventLog` |
| **CommandIdempotency** | Incoming Commands | Duplicate command execution | `ClientRequests` |

---

## Design Principles

### 1. **Isolation & Optionality**
- Each component is independent and optional
- Add only what you need for each service
- No forced dependencies

### 2. **Automatic Configuration**
- Uses `IEntityTypeConfiguration<T>` pattern
- No manual `OnModelCreating` setup required
- Assembly auto-discovery via `ApplyConfigurationsFromAssembly`

### 3. **Flexible Registration Order**
- Components can be registered in any order
- Singleton `QltDbContextConfiguration` pattern
- Works regardless of registration sequence

### 4. **Layer Separation**
- Abstractions in `EventBus` (no persistence knowledge)
- Implementation in `Persistence.EF` (EF Core agnostic)
- Specific config in `Persistence.EF.Relational` (SQL/PostgreSQL)

### 5. **Extensibility**
- Easy to add support for Cosmos DB, MongoDB, etc.
- Convention-based API: `AddCommandIdempotency<TContext>()`
- Same method name, different namespace

---

## Database Migrations

### Initial Setup
```bash
# Add migration
dotnet ef migrations add AddEventBusComponents -p YourService.Infrastructure

# Update database
dotnet ef database update -p YourService.Infrastructure
```

### Tables Created

**With all three components:**
```sql
-- EventBus.Idempotency
CREATE TABLE ProcessedIntegrationEvents (
    EventId UNIQUEIDENTIFIER,
    HandlerName NVARCHAR(200),
    ProcessedDate DATETIME2,
    PRIMARY KEY (EventId, HandlerName)
);

-- EventBus.TransactionalOutbox
CREATE TABLE IntegrationEventLog (
    EventId UNIQUEIDENTIFIER PRIMARY KEY,
    EventTypeName NVARCHAR(200),
    Content NVARCHAR(MAX),
    CreationTime DATETIME2,
    State INT,
    TimesSent INT,
    TransactionId UNIQUEIDENTIFIER
);

-- CommandIdempotency
CREATE TABLE ClientRequests (
    Id UNIQUEIDENTIFIER PRIMARY KEY,
    Name NVARCHAR(200),
    Time DATETIME2
);
```

---

## Testing

### Unit Testing
Each component provides testable interfaces:
- `IEventIdempotencyService`
- `IIntegrationEventService`
- `IRequestManager`

### Integration Testing
Test with in-memory database or test containers:
```csharp
services.AddQltSystemDBContext<TestContext>(
    conf => conf.RegisterModelsFromAssembly(...),
    options => options.UseInMemoryDatabase("TestDb")
);
```

---

## Troubleshooting

### Build Errors

**Error**: `Cannot find assembly for configuration`
- Ensure `AddCommandIdempotency` / `AddEventIdempotency` / `AddTransactionalOutbox` is called
- Check that assembly registration is working

**Error**: `Table 'X' doesn't exist`
- Run migrations: `dotnet ef database update`
- Verify component is registered before migration generation

### Runtime Errors

**Issue**: Events processed multiple times
- Check `AddEventIdempotency` is registered
- Verify handler implements correct interface

**Issue**: Events not being published
- Verify `AddTransactionalOutbox` is registered
- Check `AddTransactionalOutboxBehavior()` is called in MediatR config
- Verify background publisher is running

**Issue**: Commands executed multiple times
- Check `AddCommandIdempotency` is registered
- Verify correlation ID is consistent across retries
- Check `ClientRequests` table has unique constraint

---

## Performance Considerations

### Database Impact
Each pattern adds minimal overhead:
- **Idempotency**: 1 SELECT per event/command
- **Outbox**: 1 INSERT per event + background SELECT/UPDATE
- **Command Idempotency**: 1 SELECT + 1 INSERT per command

### Optimization Tips
1. **Indexes**: Already created by configuration
2. **Cleanup**: Implement periodic cleanup for old records
3. **Partitioning**: For very high-volume scenarios

---

## Migration from Old Implementation

### Before (monolithic IntegrationEventLogEF)
```csharp
services.AddIntegrationEventLogService<AccountContext>();
modelBuilder.UseIntegrationEventLogs();
```

### After (isolated components)
```csharp
services.AddTransactionalOutbox<AccountContext>(...);
services.AddEventIdempotency<AccountContext>();
// Configuration is automatic
```

---

## References

- [Microsoft eShopOnContainers](https://github.com/dotnet-architecture/eShopOnContainers)
- [Transactional Outbox Pattern](https://microservices.io/patterns/data/transactional-outbox.html)
- [Idempotent Consumer Pattern](https://www.enterpriseintegrationpatterns.com/patterns/messaging/IdempotentReceiver.html)
- [Entity Framework Core - Configuration](https://learn.microsoft.com/en-us/ef/core/modeling/)
