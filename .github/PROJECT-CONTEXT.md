# QltS - Project Context & Architectural Principles

## 🎯 Project Mission

This repository contains the **foundational architecture** for the QltS system. The primary objective is to develop **stable base components** that:

- ✅ **Minimize breaking changes** when business services are built on top
- ✅ **Allow platform evolution** without drastically impacting existing services
- ✅ **Provide solid abstractions** for technological changes

## 🏗️ Core Architectural Principle

> **"Platform evolves, services sleep peacefully"** 😴🚀

### The Challenge

We are building the architectural foundation **before** knowing all final technology decisions:

- **Event Bus**: Currently RabbitMQ, but could be Azure EventHub, MassTransit, or others
- **Persistence**: EF Core with SQL Server/PostgreSQL, but Cosmos DB may be added
- **Features**: Incomplete by design (e.g., no retries in EventBus yet)

### The Solution

**Infrastructure changes must be transparent to business services** through:

1. **Dependency Inversion**: Services depend on abstractions, not implementations
2. **Layered Architecture**: Clear separation between contracts and implementations
3. **Extensibility by Composition**: Decorators over inheritance
4. **Versioned Abstractions**: Semantic versioning guarantees stability

---

## 📐 Architecture Layers

```
┌─────────────────────────────────────────────┐
│  Qlt.Services.* (Business Logic)            │  ← Services built by teams
│       ↓ (Depends only on abstractions)      │
├─────────────────────────────────────────────┤
│  Qlt.Platform.*.Abstractions                │  ← Stable contracts (NuGet)
│       ↓                                      │
├─────────────────────────────────────────────┤
│  Qlt.Platform.* (Core Logic)                │  ← Can evolve
│       ↓                                      │
├─────────────────────────────────────────────┤
│  Qlt.Platform.*.RabbitMQ / EventHub / etc.  │  ← Swappable implementations
└─────────────────────────────────────────────┘
```

### Current Structure

```
src/
├── Qlt.Platform/                       ← Infrastructure Components
│   ├── Platform.Messaging.Abstractions ← Stable contracts
│   ├── Platform.Messaging              ← Core logic
│   ├── Platform.Messaging.EntityFramework
│   ├── Platform.Messaging.Redis
│   ├── Platform.EventBus               ← Event bus abstraction
│   ├── Platform.EventBus.RabbitMQ      ← Swappable implementation
│   ├── Platform.Persistence.EntityFramework
│   └── Platform.Persistence.EntityFramework.Relational
│
├── Qlt.Domain/                         ← DDD Building Blocks
│   ├── Domain.Core                     ← Entity, ValueObject, Result
│   └── Domain.Application              ← Behaviors, Validation
│
└── Qlt.Services/                       ← Business Microservices
    ├── Services.Accounts               ← Example service
    └── Services.Notifications
```

---

## 🔄 Example: Event Bus Evolution (Non-Breaking)

### Scenario: Switching from RabbitMQ to Azure EventHub

**Today (RabbitMQ):**
```csharp
// AppHost configuration
services.AddRabbitMqEventBus(config);

// Service code (unchanged)
public class MyHandler : IIntegrationEventHandler<MyEvent>
{
    public async Task Handle(MyEvent @event, CancellationToken ct)
    {
        // Business logic
    }
}
```

**Tomorrow (Azure EventHub):**
```csharp
// AppHost configuration - ONLY CHANGE
services.AddAzureEventHubEventBus(config);

// Service code - NO CHANGES NEEDED
public class MyHandler : IIntegrationEventHandler<MyEvent>
{
    public async Task Handle(MyEvent @event, CancellationToken ct)
    {
        // Business logic (identical)
    }
}
```

**Impact:** ✅ **Zero service recompilation** ✅ **Zero code changes in services**

---

## 📦 Component Status & Evolution Roadmap

| Component | Status | Planned Evolution |
|-----------|--------|-------------------|
| **Domain.Core** | ✅ Stable | Add Specification pattern, more DDD patterns |
| **Platform.Messaging** | ✅ Functional | Add retries, circuit breaker, dead letter queue |
| **Platform.EventBus** | ⚠️ Basic | Switch tech stack (RabbitMQ → EventHub/MassTransit?) |
| **Platform.Persistence.EF** | ✅ Functional | Add Cosmos DB support, read replicas |
| **Idempotency Pattern** | ✅ Complete | Performance optimizations, cache layer |
| **Outbox Pattern** | ✅ Complete | Batch processing, parallelization |
| **Idempotent Consumer** | ✅ Complete | TTL configuration, Redis optimization |

---

## 🛡️ Non-Breaking Evolution Strategies

### 1. Dependency Inversion (Already Applied)

```csharp
// ✅ CORRECT: Services depend on abstractions
public class MyService
{
    private readonly IEventBus _eventBus;  // Interface from Platform.Abstractions
}

// ❌ WRONG: Direct dependency on implementation
public class MyService
{
    private readonly RabbitMqEventBus _eventBus;  // Concrete class
}
```

### 2. Immutable Contracts

- Interfaces in `*.Abstractions` projects are **never modified** after publication
- New features = new interfaces (e.g., `IEventBusV2`) or optional methods with default implementations

### 3. Extensibility via Decorators

```csharp
// ❌ WRONG: Inheritance (fragile)
public class MyEventBus : EventBus
{
    // Changes break derived classes
}

// ✅ CORRECT: Decorator (extensible)
public class RetryEventBusDecorator : IEventBus
{
    private readonly IEventBus _inner;
    
    public async Task PublishAsync<T>(T @event)
    {
        await Polly.Retry(() => _inner.PublishAsync(@event));
    }
}
```

### 4. Feature Flags for Gradual Rollout

```csharp
// Allow gradual adoption of new features
services.AddEventBus(options =>
{
    options.EnableRetries = false;  // Default: false (no breaking change)
    options.MaxRetries = 3;
});
```

### 5. Semantic Versioning for Abstractions

```
Platform.Messaging.Abstractions
├── 1.0.0 → Initial release
├── 1.1.0 → Add optional IDomainEventHandler.HandleErrorAsync() with DIM
├── 1.2.0 → Add IIdempotentRequest.GetCacheKey()
└── 2.0.0 → Breaking: Rename ICommand to IApplicationCommand
```

**Guarantee:** Services can stay on v1.x and keep working even when v2.x exists.

---

## 🎓 Decision-Making Framework

Before adding/changing base components, ask:

### ✅ Compatibility Check
1. **Does it break existing services?** → Then it needs a new version or abstraction
2. **Is it optional?** → Use Feature Toggle or separate package
3. **Does it change behavior?** → Must be opt-in, not automatic
4. **Does it depend on specific tech?** → Move to `*.Technology` project

### ✅ Extensibility Check
1. **Can new features be added via decorators?** → Prefer over modifying base
2. **Can services choose versions?** → Use NuGet semantic versioning
3. **Is configuration backward-compatible?** → Defaults must not break existing code

### ✅ Abstraction Check
1. **Is the interface technology-agnostic?** → Don't leak RabbitMQ/SQL details
2. **Can it work with multiple implementations?** → Design for polymorphism
3. **Does it belong in Domain or Platform?** → Keep Domain pure DDD

---

## 🚀 Success Metrics

The base architecture is successful if:

1. ✅ **Services created 6 months ago require ZERO changes** when we update Platform packages
2. ✅ **We can switch RabbitMQ → EventHub in 1 day** without touching services
3. ✅ **New Platform features are opt-in**, not breaking by default
4. ✅ **Service tests do NOT fail** when upgrading Platform dependencies

---

## 📝 Current Architectural Decisions (ADRs)

### ADR-001: IDomainEvent Lives in Domain.Core
**Decision:** Move `IDomainEvent` from `Platform.Messaging.Abstractions` to `Domain.Core`

**Rationale:**
- Domain Events are a DDD pattern, not a messaging infrastructure concern
- Domain.Core must be pure and not depend on Platform
- Follows Dependency Inversion Principle (Platform depends on Domain, not vice versa)

**Impact:** ✅ Domain.Core is now 100% autonomous and can be reused in any context

---

### ADR-002: Namespaces Reflect Architectural Concerns
**Decision:** 
- `Qlt.Platform.*` = Technical infrastructure (messaging, persistence, telemetry)
- `Qlt.Domain.*` = DDD patterns (Entity, ValueObject, Result, Events)
- `Qlt.Services.*` = Business microservices

**Rationale:**
- Clear separation of concerns
- Prevents "junk drawer" anti-pattern (avoid generic names like "Shared", "Common")
- Makes dependencies explicit

**Impact:** ✅ New developers can navigate codebase by architectural layer

---

### ADR-003: Redundant References Must Be Eliminated
**Decision:** If Project A → B and B → C, then A must NOT reference C directly

**Rationale:**
- Reduces coupling
- Clarifies dependency graph
- Prevents circular dependencies

**Example:**
```
❌ BEFORE: Services.Accounts.Application → Platform.EventBus (redundant)
           Services.Accounts.Application → Domain.Application → Platform.Messaging → Platform.EventBus

✅ AFTER:  Services.Accounts.Application → Domain.Application (only)
```

**Impact:** ✅ Removed 16 redundant references across solution

---

### ADR-004: Decorator Pattern for Cross-Cutting Concerns (Telemetry)
**Decision:** Use Decorator Pattern instead of Template Method (base classes) for adding telemetry to event handlers

**Context:**
- Previously: `DomainEventHandler<T>` base class with `HandleAsync` abstract method
- Problem: Developers could accidentally override `Handle()` instead of `HandleAsync()`, losing telemetry
- Risk: Inconsistent implementation compared to Integration Events (which used decorators)

**Rationale:**
- **Safety**: Impossible to bypass telemetry (no method to override incorrectly)
- **Consistency**: Same pattern for both Integration Events and Domain Events
- **Extensibility**: Multiple decorators can be chained (retry, caching, etc.)
- **Testability**: Handlers can be tested without telemetry infrastructure
- **Single Responsibility**: Handler = business logic, Decorator = cross-cutting concern
- **Composition over Inheritance**: Aligns with PROJECT-CONTEXT.md principles

**Implementation:**
```csharp
// Handler (pure business logic)
public class MyDomainEventHandler : IDomainEventHandler<MyEvent>
{
    public async Task Handle(MyEvent notification, CancellationToken ct)
    {
        // Business logic only
    }
}

// Decorator (automatically applied via DI)
services.AddDomainEventTelemetryDecorator();
```

**Impact:**
- ✅ Removed 1 base class (`DomainEventHandler<T>`)
- ✅ Refactored 3 existing handlers (OrganizationCreated, TestEntityCreated, TestEntityUpdated)
- ✅ Created 1 decorator (`TelemetryDomainEventHandlerDecorator<T>`)
- ✅ Updated 4 DI registrations (Accounts, Notifications, Identity, TestWebApi)
- ✅ Consistent with Integration Events pattern
- ✅ Zero risk of telemetry bypass

**Date:** 2025-01-24

---

### ADR-005: Domain Events Dispatched After Transaction Commit (+ Architectural Refactoring)
**Decision:** Domain Events are dispatched by `MessagingBehavior` AFTER transaction commit, not during `SaveEntitiesAsync()`

**Context:**
- Previously: Domain Events dispatched in `QltDBContext.SaveEntitiesAsync()` AFTER `SaveChanges()` but BEFORE `CommitAsync()`
- Problem: Handler failures caused full transaction rollback of already-saved data
- Risk: Data inconsistency if handler modifies other entities or publishes to outbox before failing

**Rationale:**
- **Data Integrity**: Changes are guaranteed to be persisted before events dispatch
- **DDD Best Practice**: Domain Event Handlers should NOT modify aggregates (enforced by architecture)
- **Consistency**: Same pattern as Integration Events (dispatched after commit in `MessagingBehavior`)
- **Eventual Consistency**: Handlers publish Integration Events to outbox for cross-service communication
- **Robustness**: If handler fails, data is already committed (no rollback)

**Architectural Refactoring (Post-Implementation Review):**

During implementation review, two critical architectural violations were identified and fixed:

1. **SRP Violation**: `ITransactionalUnitOfWork` was holding Domain Events (`GetPendingDomainEvents()`)
   - **Issue**: Mixed transaction management with domain event collection (two responsibilities)
   - **Fix**: Created `IDomainEventCollector` interface to separate concerns

2. **Layer Violation**: `DomainEventsDispatcherExtension` was in `Platform.Persistence.EntityFramework`
   - **Issue**: Dispatch is a messaging concern, not persistence
   - **Fix**: Moved to `Platform.Messaging.EntityFramework` (correct layer)

3. **DDD Purity Improvement**: Storing events prematurely extracted from entities
   - **Issue**: Events extracted in `SaveEntitiesAsync()` before transaction commit
   - **Fix**: Store `List<Entity>` instead of `List<IDomainEvent>`, events remain in entities until dispatch

**Implementation (Refactored Architecture):**

```csharp
// 1. IDomainEventCollector Interface - Separates responsibilities (SRP)
public interface IDomainEventCollector
{
    IReadOnlyList<Entity> GetEntitiesWithPendingEvents();
}

// 2. QltDBContext - Implements IDomainEventCollector, stores entities (not events)
public async Task<bool> SaveEntitiesAsync(CancellationToken ct = default)
{
    // Collect entities with events (events remain in entities)
    _entitiesWithEvents = ChangeTracker
        .Entries<Entity>()
        .Where(x => x.Entity.DomainEvents != null && x.Entity.DomainEvents.Any())
        .Select(x => x.Entity)
        .ToList();

    await base.SaveChangesAsync(ct);  // Persist changes
    return true;                       // DO NOT dispatch yet
}

public IReadOnlyList<Entity> GetEntitiesWithPendingEvents()
{
    var entities = _entitiesWithEvents ?? new List<Entity>();
    _entitiesWithEvents = null;  // Clear after reading
    return entities;
}

// 3. DomainEventsDispatcherExtension - Moved to Messaging layer, centralized telemetry
public static class DomainEventsDispatcherExtension
{
    public static async Task DispatchDomainEventsAsync(
        this IMediator mediator,
        IReadOnlyList<Entity> entities,
        CancellationToken ct = default)
    {
        using var activity = ActivityHelper.StartActivity("DispatchDomainEvents");

        // Extract events AFTER commit
        var domainEvents = entities
            .SelectMany(e => e.DomainEvents ?? Array.Empty<IDomainEvent>())
            .ToList();

        // Clear events from entities
        foreach (var entity in entities)
            entity.ClearDomainEvents();

        // Dispatch with centralized telemetry
        ApplicationDiagnostics.DomainEventsDispatched.Add(domainEvents.Count);

        foreach (var domainEvent in domainEvents)
            await mediator.Publish(domainEvent, ct);
    }
}

// 4. MessagingBehavior - Uses IDomainEventCollector + extension method
public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken ct)
{
    var response = await next(ct);  // Transaction already committed here ✅

    if (_unitOfWork.LastCommittedTransactionId is { } transactionId)
    {
        // Dispatch Domain Events FIRST (using IDomainEventCollector)
        if (_eventCollector is not null)
        {
            var entities = _eventCollector.GetEntitiesWithPendingEvents();
            if (entities.Count > 0)
            {
                await _mediator.DispatchDomainEventsAsync(entities, ct);
            }
        }

        // Publish Integration Events SECOND
        await _outboxService.PublishEventsThroughEventBusAsync(transactionId);
    }

    return response;
}
```

**DDD Restriction (enforced by architecture):**
Domain Event Handlers MUST NOT modify aggregates directly. Instead:
```csharp
// ✅ CORRECT: Publish Integration Event for eventual consistency
public async Task Handle(OrganizationCreatedDomainEvent evt, CancellationToken ct)
{
    await _eventBus.PublishAsync(new OrganizationCreatedIntegrationEvent(...));
}

// ❌ INCORRECT: Modify aggregate (no active transaction)
public async Task Handle(OrganizationCreatedDomainEvent evt, CancellationToken ct)
{
    var user = await _context.Users.FindAsync(evt.UserId);
    user.AddOrganization(evt.OrgId);  // ❌ Will fail or create new transaction
    await _context.SaveChangesAsync();
}
```

**Impact:**
- ✅ **CREATED** `IDomainEventCollector` interface - Separates event collection from transaction management (SRP)
- ✅ **CREATED** `DomainEventsDispatcherExtension` in `Platform.Messaging.EntityFramework` - Centralized telemetry
- ✅ **DELETED** `DomainEventsDispatcherExtension` from `Platform.Persistence.EntityFramework` - Removed from wrong layer
- ✅ **REFACTORED** `QltDBContext` - Implements `IDomainEventCollector`, stores `List<Entity>` (events remain in entities)
- ✅ **CLEANED** `ITransactionalUnitOfWork` - Removed `GetPendingDomainEvents()` (no longer responsible for Domain Events)
- ✅ **REFACTORED** `MessagingBehavior` - Injects `IDomainEventCollector`, uses extension method for centralized telemetry
- ✅ **UPDATED** Test mocks - Implement `GetEntitiesWithPendingEvents()`
- ✅ Data integrity guaranteed (transaction committed before event dispatch)
- ✅ Follows DDD principles (handlers cannot modify aggregates, events live in entities)
- ✅ Separation of Responsibilities: `ITransactionalUnitOfWork` = transactions only, `IDomainEventCollector` = events only
- ✅ Layer Separation: Dispatch logic in `Platform.Messaging.EntityFramework` (correct layer)
- ✅ Consistent with Integration Events pattern
- ✅ Centralized telemetry preserved (ActivitySource + ApplicationDiagnostics)
- ⚠️ Breaking change for handlers that modify aggregates (must use Integration Events instead)

**Migration Guide:**
If existing Domain Event Handler modifies aggregates:
```csharp
// BEFORE (violates DDD):
public async Task Handle(MyDomainEvent evt, CancellationToken ct)
{
    var entity = await _context.Entities.FindAsync(evt.Id);
    entity.DoSomething();
    await _context.SaveChangesAsync();  // ❌ Will fail
}

// AFTER (correct DDD):
public async Task Handle(MyDomainEvent evt, CancellationToken ct)
{
    // Publish Integration Event for cross-aggregate changes
    await _eventBus.PublishAsync(new MyIntegrationEvent(evt.Id));
}

// In another bounded context/service:
public async Task Handle(MyIntegrationEvent evt, CancellationToken ct)
{
    var command = new UpdateEntityCommand(evt.Id);
    await _mediator.Send(command);  // ✅ New transaction
}
```

**Date:** 2025-01-25

---

## 📚 Reference Documentation

For detailed patterns implementation, see:
- [CQRS Pattern](../docs/02-cqrs-pattern.md)
- [Domain Events Pattern](../docs/03-domain-events-pattern.md)
- [Idempotency Pattern](../docs/04-idempotency-pattern.md)
- [Transactional Outbox Pattern](../docs/05-transactional-outbox-pattern.md)
- [Idempotent Consumer Pattern](../docs/06-idempotent-consumer-pattern.md)
- [Unit of Work Pattern](../docs/07-unit-of-work-pattern.md)
- [Pipeline Behavior Pattern](../docs/08-pipeline-behavior-pattern.md)

---

## 🤝 Contributing to Base Components

### Before Modifying Platform Components:
1. Check if there are services using the component
2. Ensure backward compatibility
3. Consider creating a new abstraction instead of modifying existing ones
4. Make new features optional/configurable
5. Update this document with new ADRs

### Golden Rules:
- **Never break services** - Changes must be backward-compatible
- **Abstract early** - Don't leak implementation details into interfaces
- **Version carefully** - Follow semantic versioning strictly
- **Test extensively** - Run all service tests before merging Platform changes

---

**Last Updated:** 2025-01-24  
**Maintained By:** Architecture Team  
**Review Cycle:** Quarterly or when major architectural decisions are made
