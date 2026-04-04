# Developer Guide: Known Gaps and Limitations

## Overview

Current known limitations and planned improvements across the platform. Items are tracked here until resolved, at which point the relevant architecture docs are updated.

---

## Active Gaps

### Authorization Behavior

**Severity**: Medium  
**Area**: Pipeline Behaviors

No automatic authorization checks in the pipeline. Handlers must manually verify permissions.

**Planned**: Create `AuthorizationBehavior` with `IAuthorizedCommand` marker interface.

---

### Repository Nullable Safety

**Severity**: Low  
**Area**: `Platform.Persistence.EntityFramework.Relational`

`Repository<T>.GetAsync(Guid id)` returns `Task<T>` (non-nullable) but `SingleOrDefaultAsync` can return null. Callers trust the non-nullable return type and skip null checks.

**Fix**: Change return type to `Task<T?>`.

---

### Entity Base Class Uses [NotMapped]

**Severity**: Low (accepted trade-off)  
**Area**: `Domain.Core`

`Entity.cs` uses `[NotMapped]` from `System.ComponentModel.DataAnnotations.Schema` on `_domainEvents` and `AuditLogs`. While it's a BCL attribute (not EF-specific), it is semantically a persistence directive in the pure domain layer.

**Status**: Acceptable pragmatism. Monitor — if Domain.Core accumulates more persistence attributes, refactor to Fluent API `.Ignore()`.

---

## Resolved Items

| Item | Resolution | Date |
|------|-----------|------|
| Missing Outbox Background Processor | `OutboxProcessor` with configurable polling, batch size, retry | ✅ |
| Domain Events dispatched outside transaction | Intentional design — dispatched inside transaction by `DomainEventProcessorBehavior` | ✅ |
| Idempotency marked before handler | Changed to mark after success via change tracker | ✅ |
| Manual inbox implementation | Automatic decorators (`IdempotencyIntegrationEventDecorator`, `IdempotencyIntegrationEventEfDecorator`) | ✅ |
| Domain depends on Platform | `IDomainEvent` moved to `Domain.Core` | ✅ |
| Template Method for telemetry | Replaced with `INotificationPublisher` behavior (ADR-004) | ✅ |
| ValidationBehavior throws exception | Returns `Result.ValidationFailure` for Result-based responses | ✅ |
| Result lacks functional composition | `Map`, `Bind`, `Match` + `ValidationFailure` factories added | ✅ |
| Outbox concurrency (duplicate publishing) | Atomic claim pattern with `ExecuteUpdateAsync` + `RowVersion` | ✅ |
| No query abstractions (CQRS) | `IQuery<T>`, `IQueryHandler<T,R>`, `TelemetryQueryBehavior` implemented in `Platform.Messaging.Abstractions` and `Platform.Messaging` | ✅ |
