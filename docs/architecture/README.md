# Architecture Documentation

Technical architecture documentation for the Sirius platform. Each document covers a specific architectural area with implementation details, design decisions, and code references. All documents reflect the current implementation under `src/` and use Mermaid diagrams.

For high-level principles, ADRs, and project context see [Principles & ADRs](00-principles-and-adrs.md).

---

## Big Picture

```mermaid
flowchart LR
    subgraph SVC["Service (e.g. Accounts)"]
        API["Minimal API"] --> PIPE["AddSiriusMediator pipeline<br/>Logging → Idempotency → Outbox publish →<br/>Transaction → Validation → Domain events → Handler"]
        PIPE --> WDB[("Write DbContext<br/>business data + IdempotencyEntry + OutboxMessage")]
        API --> QRY["Queries"] --> RDB[("Read DbContext<br/>no tracking")]
        OP["OutboxProcessor"] --> WDB
    end
    PIPE -- "after commit" --> BUS[["Event bus<br/>Service Bus topic sirius-event-bus"]]
    OP -- "retries" --> BUS
    BUS --> CONS["Other services<br/>telemetry → idempotency → handler → command"]
```

---

## Isolation and Abstractions

Each project knows as little as possible, and dependencies only point towards more stable, more abstract projects. Business code (aggregates, handlers) only uses level-2 contracts; each service's Infrastructure project references the level-4 implementations just to register them. Services never reference each other — they communicate through integration events published in `Sirius.Contracts.*` projects.

```mermaid
%%{init: {"themeVariables": {"fontSize": "11px"}, "flowchart": {"nodeSpacing": 12, "rankSpacing": 25, "padding": 4}}}%%
flowchart BT
    subgraph F[" "]
        LF["<b>1. Foundation</b><br/>no project dependencies"]:::lvl
        DC["Domain.Core"]
        PC["Platform.Core"]
        TEL["Platform.Telemetry"]
    end

    subgraph C[" "]
        LC["<b>2. Abstractions</b><br/>no EF providers, no transports"]:::lvl
        ABS["Messaging.Abstractions<br/><i>contracts only</i>"]
        EB["EventBus<br/><i>IEventBus</i>"]
        PEF["Persistence.EntityFramework<br/><i>EF abstractions, no provider</i>"]
    end

    subgraph L[" "]
        LL["<b>3. Core logic</b><br/>persistence-ignorant"]:::lvl
        MSG["Messaging<br/><i>behaviors, decorators, POCOs</i>"]
        DA["Domain.Application<br/><i>logging, validation</i>"]
    end

    subgraph I[" "]
        LI["<b>4. Implementations</b><br/>swappable"]:::lvl
        RED["Messaging.Redis"]
        MEF["Messaging.EntityFramework<br/><i>generic TContext</i>"]
        SB["EventBus.ServiceBus"]
        RMQ["EventBus.RabbitMQ"]
        REL["Persistence.EF.Relational<br/><i>only project with DB providers</i>"]
    end

    ABS ==> DC
    ABS ==> PC
    PEF ==> TEL
    MSG ==> ABS
    DA ==> MSG

    RED --> MSG
    MEF --> MSG
    SB --> EB
    RMQ --> EB
    REL --> PEF

    LC ~~~ LF
    LL ~~~ LC
    LI ~~~ LL

    classDef lvl fill:none,stroke:none,text-align:left
```

The four tiers group projects by dependency role. The full dependency rules and the more detailed five-layer messaging and persistence model are in [Messaging & Persistence Layers](01-messaging-persistence-layers.md).

### Building blocks

| Project | Responsibility | Docs |
|---|---|---|
| `Domain.Core` | DDD seed work: entities, value objects, domain events | [Domain events](03-domain-events-consistency.md) |
| `Domain.Application` | Logging and validation behaviors | [Pipeline](02-command-pipeline.md) |
| `Platform.Core` | `Result` / `Error` (railway-oriented) | [Pipeline](02-command-pipeline.md) |
| `Platform.Messaging.Abstractions` | Command, query, event and service contracts | [Layers](01-messaging-persistence-layers.md) |
| `Platform.Messaging` | Idempotency and telemetry behaviors, POCOs | [Idempotency](04-idempotency.md) |
| `Platform.Messaging.EntityFramework` | `AddSiriusMediator`, transactions, outbox | [Outbox](05-transactional-outbox.md) |
| `Platform.Messaging.Redis` | Redis fast path for idempotency | [Idempotency](04-idempotency.md) |
| `Platform.Persistence.EntityFramework` | EF Core abstractions (unit of work) | [Layers](01-messaging-persistence-layers.md) |
| `Platform.Persistence.EntityFramework.Relational` | Write/read `DbContext`s, mappings, migrations | [Read model](08-cqrs-read-model.md) |
| `Platform.EventBus` | `IEventBus` abstraction | [Event bus](07-event-bus.md) |
| `Platform.EventBus.ServiceBus` | Azure Service Bus transport (default) | [Event bus](07-event-bus.md) |
| `Platform.EventBus.RabbitMQ` | RabbitMQ transport (alternative) | [Event bus](07-event-bus.md) |
| `Platform.Telemetry` | OpenTelemetry sources and meters | [Telemetry](06-telemetry.md) |
| `Platform.ServiceDefaults` | Aspire defaults, JWT auth, OpenAPI | [Configuration](../developer-guide/01-service-configuration.md) |
| `Platform.WebApi` | `Result` → HTTP, exception middleware | [Pipeline](02-command-pipeline.md#http-mapping) |
| `Platform.Infrastructure` | Current user context (`IUserHttpContext`) | [Configuration](../developer-guide/01-service-configuration.md) |

### Swap points

| Concern | Abstraction | Implementations | What changes in a service when swapping |
|---|---|---|---|
| Event bus | `IEventBus`, `IIntegrationEventHandler<T>` | Azure Service Bus, RabbitMQ | One registration line (`AddServiceBusEventBus` → `AddRabbitMqEventBus`) and the project reference — handlers and events are untouched |
| Idempotency store | `IIdempotencyService` | EF Core (transactional, source of truth) decorating Redis (fast path) | Builder calls only (`AddIdempotencyEntityFramework`, `AddRedisIdempotencyCache`) |
| Outbox | `IEventBusOutboxService` | EF Core outbox + `OutboxProcessor` | None — domain event handlers call `AddAndSaveEventAsync` |
| Persistence | `ITransactionalUnitOfWork`, `IRepository<T>` | `SiriusDBContext` / `SiriusReadDbContext` (relational) | The service's `DbContext` base class; a new backend would be a new `Platform.Persistence.*` project |
| Telemetry | Behaviors, decorators, `INotificationPublisher` | OpenTelemetry | None — registered by `AddSiriusMediator` |

---

## Architecture Health Summary

| Pattern | Health | Notes |
|---------|--------|-------|
| Single-Transaction Pipeline | ✅ Excellent | Command + domain events + idempotency entry + outbox in one atomic transaction |
| Pipeline Behavior Chain | ✅ Excellent | Order centralized in `AddSiriusMediator`; independent behaviors with SRP |
| Transactional Outbox | ✅ Solid | Inline publishing + background processor with atomic claim; no recovery of stuck `InProgress` rows |
| Domain Events (within transaction) | ✅ Solid | Cascading support, handlers can modify aggregates |
| Idempotency (Command) | 🟡 Solid with gaps | Redis fast-path + EF source of truth; failed `Result` and Redis `processing` key can mark retries as duplicates |
| Idempotent Consumer (Integration Events) | ✅ Solid | Automatic per-handler decorators via Scrutor, transactional with EF |
| Result Pattern (ROP) | ✅ Solid | Map/Bind/Match + ValidationFailure factories; HTTP mapping is 200/400 only |
| CQRS | ✅ Solid | Commands (`ICommand<T>`) and queries (`IQuery<T>`) with read-only `DbContext` |
| Telemetry / Observability | ✅ Excellent | Activities + metrics across behaviors, handlers, outbox, event bus and migrations |
| Event Bus | ✅ Solid | Azure Service Bus with rule-based routing and DLQ; RabbitMQ alternative is less robust |
| 5-Layer Separation | ✅ Excellent | Clean boundaries, persistence-ignorant POCOs |

---

## Documents

| Document | Description |
|----------|-------------|
| [Principles & ADRs](00-principles-and-adrs.md) | Mission, decision framework, non-breaking evolution strategies, success metrics, ADRs |
| [Messaging & Persistence Layers](01-messaging-persistence-layers.md) | 5-layer architecture, responsibilities, project dependencies |
| [Command Pipeline](02-command-pipeline.md) | Result Pattern, CQRS, `AddSiriusMediator`, pipeline order, Unit of Work |
| [Domain Events & Consistency](03-domain-events-consistency.md) | Domain events, single-transaction model, cross-service event flows |
| [Idempotency](04-idempotency.md) | Command deduplication, idempotent consumer, Redis hybrid |
| [Transactional Outbox](05-transactional-outbox.md) | Outbox pattern, atomic claim, `OutboxProcessor` |
| [Telemetry](06-telemetry.md) | OpenTelemetry strategy, trace structure, metrics catalog |
| [Event Bus](07-event-bus.md) | `IEventBus` abstraction, Azure Service Bus and RabbitMQ implementations |
| [CQRS Read Model](08-cqrs-read-model.md) | Read-only `DbContext`, query-side separation |
| [Identity & BFF URL Configuration](09-identity-bff-url-configuration.md) | BFF as public identity facade, URL wiring, issuer validation |

---

## Known Gaps

| Area | Status | Details |
|------|--------|---------|
| Authorization behavior | 🟡 Planned | No authorization behavior in the pipeline; enforced by endpoint policies |
| Failed `Result` committed | 🔴 Open | `TransactionBehavior` commits failed results, persisting the idempotency entry ([02](02-command-pipeline.md#known-gaps)) |
| Redis `processing` key | 🟡 Open | Retries within `ProcessingTtl` after a failure are answered as duplicates ([04](04-idempotency.md#known-gaps)) |
| Stuck outbox rows | 🟡 Open | `InProgress` rows are never reclaimed; exhausted retries have no dead-letter ([05](05-transactional-outbox.md#known-gaps)) |
| Nested command publishing | 🟡 By design | Commands triggered by integration events publish via `OutboxProcessor` (polling latency) ([03](03-domain-events-consistency.md#integration-event--command-nested-transaction)) |
| RabbitMQ transport | 🟡 Minor | Always acks failed messages; publish retry ineffective for async failures ([07](07-event-bus.md#known-gaps)) |
| Audience validation | 🟡 Minor | APIs disable audience validation ([09](09-identity-bff-url-configuration.md#known-gaps)) |
| SQL retry-on-failure | 🟡 Minor | Services do not enable `EnableRetryOnFailure()`, so transient SQL errors are not retried ([known gaps](../developer-guide/05-known-gaps.md#no-sql-retry-on-failure)) |
| Audit log | 🟡 Planned | `AuditLog` designed but not wired ([backlog](../backlog/audit-log-proposal.md)) |
| Repository nullable safety | ✅ Resolved | `IRepository<T>.GetAsync` / `Repository<T>.GetAsync` return `T?` |
