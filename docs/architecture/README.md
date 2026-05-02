# Architecture Documentation

Technical architecture documentation for the QltS platform. Each document covers a specific architectural area with implementation details, design decisions, and code references.

For high-level principles, ADRs, and project context see [PROJECT-CONTEXT.md](../../.github/PROJECT-CONTEXT.md).

---

## Architecture Health Summary

| Pattern | Health | Notes |
|---------|--------|-------|
| Single-Transaction Pipeline | ✅ Excellent | Command + domain events + outbox in one atomic transaction |
| Pipeline Behavior Chain | ✅ Excellent | Independent behaviors with correct SRP and ordering |
| Transactional Outbox | ✅ Solid | Inline publishing + background processor for retries |
| Domain Events (within transaction) | ✅ Solid | Cascading support, handlers can modify aggregates |
| Idempotency (Command) | ✅ Solid | Redis fast-path + EF source of truth, hybrid model |
| Idempotent Consumer (Integration Events) | ✅ Solid | Automatic decorators via Scrutor |
| Result Pattern (ROP) | ✅ Solid | Map/Bind/Match + ValidationFailure factories |
| CQRS | ✅ Solid | Commands (`ICommand<T>`) and queries (`IQuery<T>`) with full pipeline support |
| Telemetry / Observability | ✅ Excellent | Activities + metrics across all behaviors and handlers |
| 5-Layer Separation | ✅ Excellent | Clean boundaries, persistence-ignorant POCOs |

---

## Documents

| Document | Description |
|----------|-------------|
| [Messaging & Persistence Layers](01-messaging-persistence-layers.md) | 5-layer architecture review, responsibilities, dependencies |
| [Command Pipeline](02-command-pipeline.md) | Result Pattern, CQRS, Unit of Work, Pipeline Behaviors |
| [Domain Events & Consistency](03-domain-events-consistency.md) | Domain Events, Eventual Consistency, single-transaction model |
| [Idempotency](04-idempotency.md) | Command deduplication, Idempotent Consumer, Redis hybrid |
| [Transactional Outbox](05-transactional-outbox.md) | Outbox Pattern, concurrency control, OutboxProcessor |
| [Telemetry](06-telemetry.md) | OpenTelemetry strategy, INotificationPublisher, decorator approach |
| [Event Bus](07-event-bus.md) | IEventBus abstraction, Azure Service Bus implementation |
| [CQRS Read Model](08-cqrs-read-model.md) | Read-only DbContext, query-side separation |
| [Identity & BFF URL Configuration](09-identity-bff-url-configuration.md) | BFF as public identity facade, correct URL wiring, IssuerUri |

---

## Known Gaps

| Area | Status | Details |
|------|--------|---------|
| Authorization behavior | 🟡 Planned | No automatic authorization checks in the pipeline |
| Repository nullable safety | 🟡 Minor | `Repository<T>.GetAsync` returns non-nullable but can be null |
