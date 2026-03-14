# QltSystem Documentation

Welcome to the QltSystem documentation. This directory contains comprehensive guides for the core components and patterns used in the system.

## 📚 Table of Contents

### Core Architecture Patterns

#### [Event-Driven Architecture Overview](EventDrivenArchitecture.md)
Complete guide to the event-driven architecture components including patterns, usage, and best practices.

---

### Component Documentation

#### 1. [EventBus.Idempotency](EventBus.Idempotency.md)
- **Pattern**: Idempotent Consumer
- **Purpose**: Prevent duplicate processing of incoming integration events
- **Use Case**: Message broker delivers same event multiple times

#### 2. [EventBus.TransactionalOutbox](EventBus.TransactionalOutbox.md)
- **Pattern**: Transactional Outbox
- **Purpose**: Guarantee reliable publishing of outgoing integration events
- **Use Case**: Publish events only if business transaction succeeds

#### 3. [CommandIdempotency](CommandIdempotency.md)
- **Pattern**: Idempotent Command
- **Purpose**: Prevent duplicate execution of incoming commands
- **Use Case**: Client retries same HTTP request multiple times

---

## 🚀 Quick Start

### Typical Service Setup

```csharp
using Core.Persistence.EF.Relational.Extensions;
using QltSystem.Core.EventBus.TransactionalOutbox.Extensions;
using QltSystem.Core.EventBus.Idempotency.Extensions;

// 1. Register DbContext
services.AddQltSystemDBContext<YourContext>(
    conf => conf
        .UseAudit()
        .SetDefaultSchema("yourservice")
        .RegisterModelsFromAssembly(typeof(AssemblyReference).Assembly),
    options => options.UseSqlServer(connectionString)
);

// 2. Add Transactional Outbox (for publishing events)
services.AddTransactionalOutbox<YourContext>(options =>
{
    options.RegisterEventTypes(typeof(YourEvents).Assembly);
});

// 3. Add Event Idempotency (for consuming events)
services.AddEventIdempotency<YourContext>();

// 4. Add Command Idempotency (for API requests)
services.AddCommandIdempotency<YourContext>();

// 5. Configure MediatR with TransactionBehavior
services.AddMediatR(cfg =>
{
    cfg.RegisterServicesFromAssemblyContaining<AssemblyReference>();
    cfg.AddTransactionalOutboxBehavior();  // Required for outbox pattern
});

// 6. Add Event Bus
builder.AddRabbitMqEventBus("eventbus")
    .AddSubscription<SomeEvent, SomeEventHandler>();
```

---

## 📖 Documentation Structure

Each component documentation includes:
- **Overview**: What problem it solves
- **Architecture**: How it's structured
- **Usage**: Step-by-step implementation guide
- **Patterns**: Best practices and recommendations
- **Testing**: Unit and integration test examples
- **Troubleshooting**: Common issues and solutions
- **Performance**: Optimization tips
- **Migration**: Guide from old implementations

---

## 🎯 When to Use What

| Scenario | Component Needed |
|----------|------------------|
| Service publishes events | `TransactionalOutbox` |
| Service consumes events | `EventBus.Idempotency` |
| Service receives HTTP commands | `CommandIdempotency` |
| Service does all of the above | All three components |

---

## 🔧 Design Principles

All components follow these principles:

1. **Isolation**: Each component is independent and optional
2. **Auto-Configuration**: Uses `IEntityTypeConfiguration<T>` - no manual setup
3. **Flexible Registration**: Works in any registration order
4. **Layer Separation**: Clean separation between abstractions and implementations
5. **Extensibility**: Easy to add support for new persistence providers

---

## 📦 Component Dependencies

```
┌─────────────────────────────────────┐
│          EventBus                   │  ← Abstractions only
│  (Interfaces, Contracts)            │
└─────────────────────────────────────┘
           ▲           ▲           ▲
           │           │           │
   ┌───────┴───┐   ┌──┴────┐   ┌──┴─────────────┐
   │ Idempot.  │   │Outbox │   │ Persistence.EF │
   └───────────┘   └───────┘   └────────────────┘
                                       ▲
                                       │
                              ┌────────┴─────────┐
                              │ Persistence.EF   │
                              │   .Relational    │
                              └──────────────────┘
```

---

## 🗄️ Database Tables

### ProcessedIntegrationEvents (EventBus.Idempotency)
Tracks which events have been processed by which handlers.

### IntegrationEventLog (EventBus.TransactionalOutbox)
Stores outgoing events for reliable publishing.

### ClientRequests (CommandIdempotency)
Tracks which commands have been executed.

---

## 🧪 Testing

### Unit Testing
```csharp
// Each component provides testable interfaces
var idempotencyService = new Mock<IEventIdempotencyService>();
var integrationService = new Mock<IIntegrationEventService>();
var requestManager = new Mock<IRequestManager>();
```

### Integration Testing
```csharp
// Use in-memory database or test containers
services.AddQltSystemDBContext<TestContext>(
    conf => conf.RegisterModelsFromAssembly(...),
    options => options.UseInMemoryDatabase("TestDb")
);
```

---

## 🐛 Common Issues

### Events Processed Multiple Times
→ Check `AddEventIdempotency<TContext>()` is registered

### Events Not Being Published
→ Verify `AddTransactionalOutboxBehavior()` in MediatR config

### Commands Executed Multiple Times
→ Check correlation ID is consistent across retries

See individual component documentation for detailed troubleshooting.

---

## 📊 Performance

Minimal overhead per operation:
- **Event Idempotency**: 1 SELECT query
- **Transactional Outbox**: 1 INSERT + background processing
- **Command Idempotency**: 1 SELECT + 1 INSERT

All tables have proper indexes for optimal performance.

---

## 🔄 Migration Guide

Migrating from old `IntegrationEventLogEF` implementation:

**Before:**
```csharp
services.AddIntegrationEventLogService<Context>();
modelBuilder.UseIntegrationEventLogs();
```

**After:**
```csharp
services.AddTransactionalOutbox<Context>(...);
services.AddEventIdempotency<Context>();
// Configuration is automatic
```

---

## 📝 Contributing

When adding new patterns or components:
1. Follow the established documentation structure
2. Include practical examples
3. Add troubleshooting section
4. Provide migration guide if replacing old implementation
5. Update this index

---

## 🔗 External Resources

- [Microsoft eShopOnContainers](https://github.com/dotnet-architecture/eShopOnContainers)
- [Microservices Patterns](https://microservices.io/patterns/)
- [Enterprise Integration Patterns](https://www.enterpriseintegrationpatterns.com/)
- [Entity Framework Core Documentation](https://learn.microsoft.com/en-us/ef/core/)

---

**Last Updated**: 2024  
**Framework**: .NET 10  
**Architecture**: Microservices with Event-Driven Communication
