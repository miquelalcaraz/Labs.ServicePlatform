# Developer Guide: Known Gaps and Limitations

## Overview

Current known limitations and planned improvements across the platform. Items are tracked here until resolved, at which point the relevant architecture docs are updated.

| Gap | Severity | Area |
|---|---|---|
| [Failed Result is committed](#failed-result-is-committed) | High | Pipeline / Idempotency |
| [Redis `processing` key survives failures](#redis-processing-key-survives-failures) | Medium | Idempotency |
| [Stuck `InProgress` outbox rows](#stuck-inprogress-outbox-rows) | Medium | Outbox |
| [Authorization behavior](#authorization-behavior) | Medium | Pipeline Behaviors |
| [Deferred publishing for nested commands](#deferred-publishing-for-nested-commands) | Low | Outbox |
| [No SQL retry-on-failure](#no-sql-retry-on-failure) | Low | Persistence |
| [RabbitMQ transport robustness](#rabbitmq-transport-robustness) | Low | Event Bus |
| [Audience validation disabled](#audience-validation-disabled) | Low | Security |
| [Audit log not implemented](#audit-log-not-implemented) | Low | Persistence |
| [Development-only security settings](#development-only-security-settings) | Low (development only) | Security |
| [Event bus transport selected in code](#event-bus-transport-selected-in-code) | Low | Event Bus |
| [Building blocks not packaged](#building-blocks-not-packaged) | Low | Distribution |
| [No end-to-end exactly-once delivery](#no-end-to-end-exactly-once-delivery) | Low (by design) | Messaging |
| [Sample shortcuts](#sample-shortcuts) | Low (sample only) | Reference implementation |
| [Entity base class uses `[NotMapped]`](#entity-base-class-uses-notmapped) | Low (accepted) | Domain.Core |

---

## Active Gaps

### Failed Result is Committed

**Severity**: High  
**Area**: `TransactionBehavior`, `IdempotencyBehavior`

`TransactionBehavior` commits whenever no exception is thrown. When validation fails or a handler returns `Result.Failure(...)`, the transaction is still committed, including the `IdempotencyEntry` that `IdempotencyBehavior` added to the change tracker. A client retrying with the same `x-requestid` receives `CreateResultForDuplicateRequest()` (typically success) although the original request failed.

**Planned**: Roll back (or skip the commit) when the response is a failed `Result`, or add the idempotency entry only after a successful response.

---

### Redis `processing` Key Survives Failures

**Severity**: Medium  
**Area**: `IdempotencyRedisService`

`MarkAsProcessingAsync` writes `idempotency:{scope}:{id}` with value `processing` (TTL `ProcessingTtl`, 30 s) before the handler runs. Nothing removes it when the command throws, and `ExistsAsync` only checks key existence, so a retry within the TTL is treated as a duplicate.

**Planned**: Delete the key on failure, or distinguish `processing` from `completed` in `ExistsAsync`.

---

### Stuck InProgress Outbox Rows

**Severity**: Medium  
**Area**: `OutboxMessageService`, `OutboxProcessor`

Rows claimed (`InProgress`) by a process that dies — or whose type is not registered and fails to deserialize — are never reclaimed. Rows that reach `MaxRetryCount` stay `FailedToPublish` without alerting. Retries have no backoff (one attempt per polling interval).

**Planned**: Reclaim `InProgress` rows older than a timeout, add a dead-letter state/metric for exhausted retries.

---

### Authorization Behavior

**Severity**: Medium  
**Area**: Pipeline Behaviors

No automatic authorization checks in the pipeline. Authorization is enforced at endpoint level with role-based policies (e.g. `AccountsAuthorizationPolicies`); commands sent from integration event handlers or other entry points are not authorized.

**Planned**: Create `AuthorizationBehavior` with `IAuthorizedCommand` marker interface.

---

### Deferred Publishing for Nested Commands

**Severity**: Low  
**Area**: `IntegrationEventProcessorBehavior`

Commands sent from integration event handlers run inside the transaction opened by `IdempotencyIntegrationEventEfDecorator`. Their outbox rows are not flushed when `IntegrationEventProcessorBehavior` runs, so they are published by the `OutboxProcessor` on its next poll (default 30 s) instead of inline. Consistency is not affected, only latency.

---

### No SQL Retry-on-Failure

**Severity**: Low  
**Area**: Service persistence configuration

`TransactionBehavior` runs inside `CreateExecutionStrategy()`, but services configure `UseSqlServer(...)` without `EnableRetryOnFailure()`, so the default (non-retrying) strategy is used and transient SQL errors are not retried.

---

### RabbitMQ Transport Robustness

**Severity**: Low (Service Bus is the default)  
**Area**: `Platform.EventBus.RabbitMQ`

Failed or unknown messages are always acknowledged (no dead-letter exchange), and the Polly publish retry wraps an async delegate with the synchronous `Execute` overload, so asynchronous broker failures are not retried.

---

### Audience Validation Disabled

**Severity**: Low  
**Area**: `AddDefaultAuthentication` (`Platform.ServiceDefaults`)

APIs configure `Audience` but set `ValidateAudience = false` and `RequireHttpsMetadata = false`. Any token issued by Identity is accepted by every API; access relies on role policies.

---

### Audit Log Not Implemented

**Severity**: Low  
**Area**: `Domain.Core`, `Platform.Persistence.EntityFramework.Relational`

`AuditLog` is a placeholder POCO and `AuditLogConfiguration` is never applied. Only `CreatedAt` / `UpdatedAt` / `RowVersion` are maintained automatically. See [audit-log-proposal.md](../backlog/audit-log-proposal.md).

---

### Development-Only Security Settings

**Severity**: Low (development only)  
**Area**: Identity, `Platform.ServiceDefaults`

IdentityServer clients use hard-coded secrets and `RequirePkce = false`, the Scalar UI clients use the implicit flow, and the BackOffice client also allows a fixed `https://localhost:5001` redirect URI. Together with the [disabled audience validation](#audience-validation-disabled), they keep the local setup simple.

**Planned**: Before any real deployment — PKCE, secrets from a vault, HTTPS metadata and audience validation.

---

### Event Bus Transport Selected in Code

**Severity**: Low  
**Area**: AppHost, service registration

The transport is chosen with a `#define` in `AppHost.cs` plus one registration line and one project reference per service, not through configuration. See [Event bus](../architecture/07-event-bus.md).

---

### Building Blocks Not Packaged

**Severity**: Low  
**Area**: Distribution

The building blocks are consumed as project references. The NuGet packaging and semantic versioning described in [Principles & ADRs](../architecture/00-principles-and-adrs.md) are design goals.

---

### No End-to-End Exactly-Once Delivery

**Severity**: Low (by design)  
**Area**: Outbox, idempotent consumers

The transactional outbox and idempotent consumers protect the implemented paths (at-least-once publishing, deduplicated handling); they do not provide end-to-end exactly-once delivery.

---

### Sample Shortcuts

**Severity**: Low (sample only)  
**Area**: Reference implementation

Emails are only logged and the activation link carries no confirmation token. See [Simplifications in the sample](../features/02-onboarding-activation-workflow.md#simplifications-in-the-sample).

---

### Entity Base Class Uses [NotMapped]

**Severity**: Low (accepted trade-off)  
**Area**: `Domain.Core`

`Entity.cs` uses `[NotMapped]` (from `System.ComponentModel.DataAnnotations.Schema`) and `[JsonIgnore]` on the `_domainEvents` field. While `[NotMapped]` is a BCL attribute (not EF-specific), it is semantically a persistence directive in the pure domain layer.

**Status**: Acceptable pragmatism. Monitor — if Domain.Core accumulates more persistence attributes, refactor to Fluent API `.Ignore()`.

---

## Resolved Items

| Item | Resolution | Date |
|------|-----------|------|
| Missing Outbox Background Processor | `OutboxProcessor` with configurable polling, batch size, retry | ✅ |
| Domain Events dispatched outside transaction | Intentional design — dispatched inside transaction by `DomainEventProcessorBehavior` | ✅ |
| Idempotency persisted before handler | Entry is added to the change tracker before the handler and persisted only with the transaction commit | ✅ |
| Manual inbox implementation | Automatic decorators (`IdempotencyIntegrationEventDecorator`, `IdempotencyIntegrationEventEfDecorator`) | ✅ |
| Domain depends on Platform | `IDomainEvent` moved to `Domain.Core` | ✅ |
| Template Method for telemetry | Replaced with decorators / `INotificationPublisher` behavior (ADR-004) | ✅ |
| ValidationBehavior throws exception | Returns `Result.ValidationFailure` for Result-based responses | ✅ |
| Result lacks functional composition | `Map`, `Bind`, `Match` + `ValidationFailure` factories added | ✅ |
| Outbox concurrency (duplicate publishing) | Atomic claim pattern with `ExecuteUpdateAsync` + `RowVersion` | ✅ |
| No query abstractions (CQRS) | `IQuery<T>`, `IQueryHandler<T,R>`, `TelemetryQueryBehavior` implemented in `Platform.Messaging.Abstractions` and `Platform.Messaging` | ✅ |
| Repository nullable safety | `IRepository<T>.GetAsync` and `Repository<T>.GetAsync` return `Task<T?>` | ✅ |
| Manual pipeline registration | `AddSiriusMediator` registers behaviors in a fixed order plus all telemetry | ✅ |
