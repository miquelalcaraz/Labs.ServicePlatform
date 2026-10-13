# Principles, Decision Framework & ADRs

## 🎯 Project Mission

This repository contains the **foundational architecture** for the Sirius lab project. The primary objective is to develop **stable base components** that:

- ✅ **Minimize breaking changes** when business services are built on top
- ✅ **Allow platform evolution** without drastically impacting existing services
- ✅ **Provide solid abstractions** for technological changes

## 🏗️ Core Architectural Principle

> **"Platform evolves, services sleep peacefully"** 😴🚀

### The Challenge

We are building the architectural foundation **before** knowing all final technology decisions:

- **Event Bus**: Azure Service Bus by default, RabbitMQ as an alternative, and possibly Event Hubs, MassTransit or others in the future
- **Persistence**: EF Core with SQL Server/PostgreSQL, but Cosmos DB may be added
- **Features**: Incomplete by design (e.g., no retries in EventBus yet)

### The Solution

**Infrastructure changes must be transparent to business services** through:

1. **Dependency Inversion**: Services depend on abstractions, not implementations
2. **Layered Architecture**: Clear separation between contracts and implementations
3. **Extensibility by Composition**: Decorators over inheritance
4. **Versioned Abstractions**: Semantic versioning guarantees stability *(design goal — components are project references today, not yet published as NuGet packages)*

---

## 📐 Architecture Layers

```
┌─────────────────────────────────────────────┐
│  Sirius.Services.* (Business Logic)            │  ← Services built by teams
│       ↓ (Depends only on abstractions)      │
├─────────────────────────────────────────────┤
│  Sirius.Platform.*.Abstractions                │  ← Stable contracts
│       ↓                                      │
├─────────────────────────────────────────────┤
│  Sirius.Platform.* (Core Logic)                │  ← Can evolve
│       ↓                                      │
├─────────────────────────────────────────────┤
│  Sirius.Platform.*.ServiceBus / RabbitMQ / etc.│  ← Swappable implementations
└─────────────────────────────────────────────┘
```

Layer responsibilities and allowed dependencies for messaging and persistence are detailed in [Messaging & Persistence Layers](01-messaging-persistence-layers.md).

### Current Structure

```
src/
├── Sirius.Platform/                       ← Infrastructure Components
│   ├── Platform.Core                   ← Result, Error, ValidationError
│   ├── Platform.Telemetry              ← OpenTelemetry, ActivitySource, Meters
│   ├── Platform.Messaging.Abstractions ← Stable contracts
│   ├── Platform.Messaging              ← Core logic, POCOs, Behaviors
│   ├── Platform.Messaging.EntityFramework ← EF-based services, Outbox, Transactions
│   ├── Platform.Messaging.Redis        ← Redis-based idempotency
│   ├── Platform.EventBus               ← Event bus abstraction
│   ├── Platform.EventBus.RabbitMQ      ← Swappable implementation
│   ├── Platform.EventBus.ServiceBus    ← Swappable implementation
│   ├── Platform.Persistence.EntityFramework ← EF abstractions
│   ├── Platform.Persistence.EntityFramework.Relational ← SQL/PostgreSQL
│   ├── Platform.WebApi                 ← API helpers, middleware
│   ├── Platform.Infrastructure         ← Cross-cutting infrastructure
│   └── Platform.ServiceDefaults        ← .NET Aspire service defaults
│
├── Sirius.Domain/                         ← DDD Building Blocks
│   ├── Domain.Core                     ← Entity, ValueObject, IDomainEvent
│   └── Domain.Application              ← Behaviors, Validation
│
├── Sirius.Contracts/                      ← Integration events shared between services
│   ├── Contracts.Accounts
│   ├── Contracts.Identity
│   └── Contracts.Notifications
│
├── Sirius.Services/                       ← Business Microservices
│   ├── Sirius.Services.Accounts        ← Api, Application, Domain, Infrastructure
│   └── Sirius.Services.Notifications   ← Api, Application, Domain, Infrastructure
│
├── Sirius.Infrastructure/                 ← Shared Infrastructure Services
│   ├── Infrastructure.Identity         ← Duende IdentityServer, Tenants, Users
│   ├── Infrastructure.Bff.BackOffice   ← YARP backend for frontend
│   └── Infrastructure.AppHost          ← .NET Aspire orchestrator
│
└── Sirius.WebApps/                        ← Frontend Applications
    └── Sirius.BackOfficeApp            ← Blazor + Radzen back office

tests/
├── Platform/
│   ├── Platform.UnitTests              ← xUnit, Moq, FluentAssertions, EF InMemory
│   ├── Platform.IntegrationTests       ← xUnit, Testcontainers (SQL Server, Service Bus)
│   ├── Platform.FunctionalTests        ← Aspire AppHost, xUnit v3
│   └── Platform.TestWebApi             ← Test Web API host (used by FunctionalTests)
└── Services/
    ├── Services.UnitTests              ← Accounts, Identity and Notifications unit tests
    └── Services.FunctionalTests        ← Aspire AppHost, xUnit v3, end-to-end API tests
```

---

## 🔄 Example: Event Bus Evolution (Non-Breaking)

### Scenario: Switching from Azure Service Bus to RabbitMQ

**Today (Azure Service Bus, default):**
```csharp
// Service registration (Infrastructure/Extensions/Extensions.cs of each service)
builder.AddServiceBusEventBus("eventbus")
    .AddSubscription<MyEvent, MyHandler>();

// Handler code
public class MyHandler : IIntegrationEventHandler<MyEvent>
{
    public async Task Handle(MyEvent @event)
    {
        // Business logic
    }
}
```

**Alternative (RabbitMQ) — Already implemented:**
```csharp
// Service registration - ONLY CHANGE (plus the Platform.EventBus.RabbitMQ project reference)
builder.AddRabbitMqEventBus("eventbus")
    .AddSubscription<MyEvent, MyHandler>();

// Handler code - NO CHANGES NEEDED
public class MyHandler : IIntegrationEventHandler<MyEvent>
{
    public async Task Handle(MyEvent @event)
    {
        // Business logic (identical)
    }
}
```

**Impact:** ✅ **Zero changes in handlers and integration events** — only the transport reference and one registration line per service change (plus the AppHost resource). See [Event Bus](07-event-bus.md).

---

## 📦 Component Status & Evolution Roadmap

| Component | Status | Planned Evolution |
|-----------|--------|-------------------|
| **Domain.Core** | ✅ Stable | Add Specification pattern, more DDD patterns |
| **Platform.Core** | ✅ Stable | Result pattern, Error types |
| **Platform.Messaging** | ✅ Functional | Add retries, circuit breaker, dead letter queue |
| **Platform.EventBus** | 🟡 Functional, known gaps | RabbitMQ + ServiceBus implementations available |
| **Platform.Persistence.EF** | ✅ Functional | Add Cosmos DB support, read replicas |
| **Idempotency Pattern** | 🟡 Functional, known gaps | Unified IIdempotencyService (DB + Cache) |
| **Outbox Pattern** | 🟡 Functional, known gaps | Batch processing, parallelization |
| **Idempotent Consumer** | ✅ Functional | TTL configuration, Redis optimization |

Open gaps are tracked in [Known gaps](../developer-guide/05-known-gaps.md).

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

*Design goal — the snippet below is illustrative; no such options exist in the code yet.*

```csharp
// Allow gradual adoption of new features
services.AddEventBus(options =>
{
    options.EnableRetries = false;  // Default: false (no breaking change)
    options.MaxRetries = 3;
});
```

### 5. Semantic Versioning for Abstractions

*Design goal — the building blocks are consumed as project references today; this is the versioning policy intended for when they are packaged as NuGets.*

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

Goals the base architecture is measured against (targets, not achieved results):

1. **Services created 6 months ago require ZERO changes** when we update Platform packages
2. **We can switch RabbitMQ → EventHub in 1 day** without touching services
3. **New Platform features are opt-in**, not breaking by default
4. **Service tests do NOT fail** when upgrading Platform dependencies

---

## 📝 Architectural Decision Records (ADRs)

New ADRs are appended to this section using the same structure: **Decision / Context / Rationale / Implementation / Impact / Date**.

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
- `Sirius.Platform.*` = Technical infrastructure (messaging, persistence, telemetry)
- `Sirius.Domain.*` = DDD patterns (Entity, ValueObject, IDomainEvent, application behaviors)
- `Sirius.Services.*` = Business microservices
- `Sirius.Contracts.*` = Integration events shared between services

`Result` / `Error` live in `Sirius.Platform` (`Platform.Core`), so both Domain and Platform can use them.

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
- **Composition over Inheritance**: Aligns with the principles in this document

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

// Telemetry publisher (registered automatically by AddSiriusMediator)
services.AddDomainEventTelemetryBehavior();
```

**Impact:**
- ✅ Removed 1 base class (`DomainEventHandler<T>`)
- ✅ Refactored 3 existing handlers (at the time: OrganizationCreated — now `Company` — TestEntityCreated, TestEntityUpdated)
- ✅ Created `TelemetryDomainEventBehavior` (custom `INotificationPublisher` replacing MediatR's default publisher)
- ✅ Updated 4 DI registrations (Accounts, Notifications, Identity, TestWebApi)
- ✅ Consistent with Integration Events pattern
- ✅ Zero risk of telemetry bypass

**Date:** 2025-01-24

---

### ADR-005: Separated Pipeline for Domain Events and Integration Events (+ Architectural Refactoring)
**Decision:** Domain Events and Integration Events are dispatched by two separate pipeline behaviors. Domain events are dispatched by `DomainEventProcessorBehavior` WITHIN the active transaction (after `SaveChangesAsync`, before `CommitAsync`). Integration events are published by `IntegrationEventProcessorBehavior` AFTER transaction commit.

**Context:**
- Previously: Domain Events dispatched in `SiriusDBContext.SaveEntitiesAsync()` mixed with persistence logic
- Problem: Handler failures caused full transaction rollback of already-saved data, and single behavior mixed two concerns
- Risk: Data inconsistency if dispatch and publishing were coupled

**Rationale:**
- **Separation of Concerns**: Two independent behaviors instead of one monolithic `MessagingBehavior`
- **Domain Events WITHIN Transaction**: Handlers can modify aggregates and add integration events to the outbox as part of the same transaction
- **Integration Events AFTER Commit**: Published to the event bus only after the transaction is guaranteed to be persisted
- **Cascading Support**: `DomainEventProcessorBehavior` re-checks for new domain events after each dispatch cycle
- **Eventual Consistency**: Domain event handlers publish Integration Events to outbox for cross-service communication

**Architectural Refactoring (Post-Implementation Review):**

During implementation review, critical architectural violations were identified and fixed:

1. **SRP Violation**: `ITransactionalUnitOfWork` was holding Domain Events (`GetPendingDomainEvents()`)
   - **Issue**: Mixed transaction management with domain event collection (two responsibilities)
   - **Fix**: Created `IDomainEntityChangeTracker` interface to separate concerns

2. **Layer Violation**: `DomainEventsDispatcherExtension` was in `Platform.Persistence.EntityFramework`
   - **Issue**: Dispatch is a messaging concern, not persistence
   - **Fix**: Moved to `Platform.Messaging` (messaging layer, no EF dependency needed)

3. **Single Behavior Split**: `MessagingBehavior` handled both domain events and integration events
   - **Issue**: Two responsibilities in one behavior, hard to configure independently
   - **Fix**: Split into `DomainEventProcessorBehavior` (inside transaction) and `IntegrationEventProcessorBehavior` (after commit)

**Implementation (Current Architecture):**

```csharp
// 1. IDomainEntityChangeTracker - Exposes tracked entities from DbContext (SRP)
public interface IDomainEntityChangeTracker
{
    IReadOnlyList<Entity> GetEntitiesWithChanges();
}

// 2. SiriusDBContext - Implements IDomainEntityChangeTracker, reads directly from ChangeTracker
public class SiriusDBContext : DbContext, ITransactionalUnitOfWork, IDomainEntityChangeTracker
{
    public IReadOnlyList<Entity> GetEntitiesWithChanges()
    {
        return ChangeTracker.Entries<Entity>()
            .Select(x => x.Entity)
            .ToList();
    }
}

// 3. DomainEventsDispatcherExtension - In Platform.Messaging, centralized telemetry
public static class DomainEventsDispatcherExtension
{
    public static async Task DispatchDomainEventsAsync(
        this IMediator mediator,
        IReadOnlyList<Entity> entities,
        CancellationToken cancellationToken)
    {
        var domainEvents = entities
            .Where(x => x.DomainEvents != null && x.DomainEvents.Any())
            .SelectMany(x => x.DomainEvents!)
            .ToList();

        if (domainEvents.Count == 0) return;

        entities.Where(x => x.DomainEvents != null)
            .ToList().ForEach(entity => entity.ClearDomainEvents());

        foreach (var domainEvent in domainEvents)
            await mediator.Publish(domainEvent, cancellationToken);
    }
}

// 4. DomainEventProcessorBehavior - INSIDE TransactionBehavior (same transaction)
//    Dispatches domain events with cascading support (re-checks for new events)
//    Simplified: telemetry and logging omitted
public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken ct)
{
    var response = await next(ct);
    var transactionId = _unitOfWork.GetCurrentTransaction()?.TransactionId ?? _unitOfWork.LastCommittedTransactionId;
    if (!transactionId.HasValue) return response;

    if (_eventCollector is not null)
    {
        var entities = _eventCollector.GetEntitiesWithChanges()
            .Where(x => x.DomainEvents != null && x.DomainEvents.Any()).ToList();

        while (entities.Count > 0)
        {
            await _mediator.DispatchDomainEventsAsync(entities, ct);
            entities = _eventCollector.GetEntitiesWithChanges()
                .Where(x => x.DomainEvents != null && x.DomainEvents.Any()).ToList();
        }
    }

    return response;
}

// 5. IntegrationEventProcessorBehavior - OUTSIDE TransactionBehavior (after commit)
//    Publishes outbox events to event bus after transaction is committed
//    Simplified: telemetry and logging omitted
public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken ct)
{
    var response = await next(ct);
    var transactionId = _unitOfWork.GetCurrentTransaction()?.TransactionId ?? _unitOfWork.LastCommittedTransactionId;

    if (transactionId.HasValue && _outboxService is not null)
    {
        await _outboxService.PublishEventsThroughEventBusAsync(transactionId.Value);
    }

    return response;
}
```

**Pipeline execution order** (event-related behaviors only; the full chain with Logging, Idempotency and Validation is in [Command pipeline](02-command-pipeline.md)):
```
IntegrationEventProcessorBehavior (OUTSIDE)
  └── TransactionBehavior (BEGIN/COMMIT)
        └── DomainEventProcessorBehavior (INSIDE)
              └── CommandHandler (business logic)
```

**Domain Event Handlers CAN modify aggregates** (they run within the active transaction):
```csharp
// ✅ CORRECT: Modify aggregates within the same transaction
public async Task Handle(CompanyCreatedDomainEvent evt, CancellationToken ct)
{
    // Save integration event to outbox (same transaction)
    await _outboxService.AddAndSaveEventAsync(new CompanyCreatedIntegrationEvent(...));
}
```

**Impact:**
- ✅ **CREATED** `IDomainEntityChangeTracker` interface in `Platform.Persistence.EntityFramework`
- ✅ **MOVED** `DomainEventsDispatcherExtension` to `Platform.Messaging`
- ✅ **DELETED** `DomainEventsDispatcherExtension` from `Platform.Persistence.EntityFramework`
- ✅ **SPLIT** `MessagingBehavior` into `DomainEventProcessorBehavior` + `IntegrationEventProcessorBehavior`
- ✅ **REFACTORED** `SiriusDBContext` - Implements `IDomainEntityChangeTracker`, reads directly from ChangeTracker
- ✅ **CLEANED** `ITransactionalUnitOfWork` - Removed `GetPendingDomainEvents()`
- ✅ Separation of Responsibilities: `ITransactionalUnitOfWork` = transactions, `IDomainEntityChangeTracker` = entity tracking
- ✅ Layer Separation: Dispatch extension in `Platform.Messaging`, behaviors in `Platform.Messaging.EntityFramework`
- ✅ Cascading domain events supported (re-check loop)
- ✅ Integration events published only after successful commit

**Date:** 2025-01-25 (Updated: 2025-06-25)

---

## 📚 Reference Documentation

For detailed patterns implementation, see:
- [Architecture Documentation](README.md) — Messaging layers, pipeline, domain events, idempotency, outbox, telemetry, event bus, CQRS read model
- [Developer Guide](../developer-guide/README.md) — Service configuration, transactional pipeline, end-to-end flow, testing, known gaps
- [Sample Feature](../features/README.md) — Customer onboarding & activation workflow, API and back office UI
- [Backlog](../backlog/README.md) — Future proposals and parked improvements

---

## 🤝 Contributing to Base Components

### Before Modifying Platform Components:
1. Check if there are services using the component
2. Ensure backward compatibility
3. Consider creating a new abstraction instead of modifying existing ones
4. Make new features optional/configurable
5. Record significant decisions as a new ADR in this document

### Golden Rules:
- **Never break services** - Changes must be backward-compatible
- **Abstract early** - Don't leak implementation details into interfaces
- **Version carefully** - Follow semantic versioning strictly
- **Test extensively** - Run all service tests before merging Platform changes

---

**Last Updated:** 2026-10-13  
**Maintained By:** Miquel Alcaraz  
**Review Cycle:** When major architectural decisions are made
