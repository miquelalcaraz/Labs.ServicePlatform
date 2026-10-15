# Developer Guide: Known Gaps and Limitations

## Overview

Current known limitations and planned improvements across the platform. Items are tracked here until resolved, at which point the relevant architecture docs are updated.

| Gap | Severity | Area |
|---|---|---|
| [Stuck `InProgress` outbox rows](#stuck-inprogress-outbox-rows) | Medium | Outbox |
| [Authorization behavior](#authorization-behavior) | Medium | Pipeline Behaviors |
| [Service Bus startup failure does not stop the host](#service-bus-startup-failure-does-not-stop-the-host) | Medium | Event Bus |
| [Deferred publishing for nested commands](#deferred-publishing-for-nested-commands) | Low | Outbox |
| [Several nested commands can publish before commit](#several-nested-commands-can-publish-before-commit) | Low (latent) | Outbox |
| [Failed nested command is not rolled back](#failed-nested-command-is-not-rolled-back) | Low (latent) | Pipeline |
| [No SQL retry-on-failure](#no-sql-retry-on-failure) | Low | Persistence |
| [RabbitMQ transport robustness](#rabbitmq-transport-robustness) | Low | Event Bus |
| [Audience validation disabled](#audience-validation-disabled) | Low | Security |
| [Audit log not implemented](#audit-log-not-implemented) | Low | Persistence |
| [Stale Service Bus subscription rules](#stale-service-bus-subscription-rules) | Low | Event Bus |
| [Idempotency entries are never purged](#idempotency-entries-are-never-purged) | Low | Idempotency |
| [Shared read context configuration](#shared-read-context-configuration) | Low | Persistence |
| [Every failed `Result` maps to `400`](#every-failed-result-maps-to-400) | Low | Web API |
| [Telemetry measurement gaps](#telemetry-measurement-gaps) | Low | Telemetry |
| [Development-only security settings](#development-only-security-settings) | Low (development only) | Security |
| [Event bus transport selected in code](#event-bus-transport-selected-in-code) | Low | Event Bus |
| [Building blocks not packaged](#building-blocks-not-packaged) | Low | Distribution |
| [No end-to-end exactly-once delivery](#no-end-to-end-exactly-once-delivery) | Low (by design) | Messaging |
| [Sample shortcuts](#sample-shortcuts) | Low (sample only) | Reference implementation |
| [Entity base class uses `[NotMapped]`](#entity-base-class-uses-notmapped) | Low (accepted) | Domain.Core |

---

## Active Gaps

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

### Service Bus Startup Failure Does Not Stop the Host

**Severity**: Medium  
**Area**: `ServiceBusEventBus`

`StartAsync` catches any exception (missing subscription name, rule configuration, processor start), logs it and returns. The service keeps running and answering HTTP requests, but it does not consume integration events; publishing failures surface later through the outbox retries.

**Planned**: Rethrow so the host fails fast, or report the state through a health check.

---

### Deferred Publishing for Nested Commands

**Severity**: Low  
**Area**: `IntegrationEventProcessorBehavior`

Commands sent from integration event handlers run inside the transaction opened by `IdempotencyIntegrationEventEfDecorator`. Their outbox rows are not flushed when `IntegrationEventProcessorBehavior` runs, so they are published by the `OutboxProcessor` on its next poll (default 30 s) instead of inline. Consistency is not affected, only latency.

---

### Several Nested Commands Can Publish Before Commit

**Severity**: Low (latent — no handler triggers it today)  
**Area**: `IntegrationEventProcessorBehavior`

The previous gap relies on the outbox rows still being unflushed when `IntegrationEventProcessorBehavior` runs. That holds for one command per integration event handler. If a handler sends two commands and the first one adds outbox rows, the second command's `SaveChangesAsync()` flushes them, and its `IntegrationEventProcessorBehavior` publishes them **before** the decorator commits — a rollback afterwards would leave a published event without its data. `ExternalUserProvisionedIntegrationEventHandler` sends two commands, but the first (`CompleteOnboardingCommand`) adds no outbox rows.

**Planned**: Skip inline publishing while the transaction is owned by an outer scope, or keep integration event handlers to a single command.

---

### Failed Nested Command Is Not Rolled Back

**Severity**: Low (latent — no handler triggers it today)  
**Area**: `TransactionBehavior`, `IdempotencyIntegrationEventEfDecorator`

`TransactionBehavior` rolls back only the transactions it owns. A command sent from an integration event handler runs inside the transaction opened by `IdempotencyIntegrationEventEfDecorator`, so when it returns a failed `Result` nothing is rolled back: `IdempotencyBehavior` discards its idempotency entry, but any other change the command tracked before failing is committed if the handler continues. Commands usually fail before changing state (validation, business checks), which keeps this latent.

**Planned**: Let integration event handlers act on the failed `Result` (throw to roll back the owned transaction), or clear the failed command's tracked changes.

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

### Stale Service Bus Subscription Rules

**Severity**: Low  
**Area**: `ServiceBusEventBus`

Rule synchronization at startup only removes the `$Default` rule and creates missing ones. Rules for events a service no longer subscribes to stay on the subscription, so those messages keep arriving and are dead-lettered as `UnknownEventType`.

---

### Idempotency Entries Are Never Purged

**Severity**: Low  
**Area**: `IdempotencyEntry`

`IdempotencyEntry` rows grow forever; there is no retention job. The `ProcessedAt` index exists to support one.

---

### Shared Read Context Configuration

**Severity**: Low  
**Area**: `AddSiriusReadDbContext`

`SiriusReadDbContextConfiguration` is a single instance per host. Two read contexts registered in the same host would share schema and model assemblies (the last `SetDefaultSchema` wins). No service has two read contexts today. The write side has the same design (`SiriusDbContextConfiguration`).

---

### Every Failed Result Maps to 400

**Severity**: Low  
**Area**: `ResultApiExtensions.ToHttpResult()` (`Platform.WebApi`)

`ToHttpResult()` returns `400 Bad Request` for any failure, including `Error.NotFound`, `Error.Conflict` and `Error.Unauthorized`. Endpoints that need `404` or `409` (e.g. `RetryNotAllowed`, `ResendNotAllowed`) map them explicitly.

---

### Telemetry Measurement Gaps

**Severity**: Low  
**Area**: `Platform.Messaging`, `Domain.Application`, outbox services

- `TelemetryCommandBehavior` / `TelemetryQueryBehavior` are the innermost behaviors, so `application.command_handlers.duration` excludes validation, transaction and domain event dispatch. End-to-end time is in `application.commands.duration`, recorded by `LoggingBehavior`.
- `LoggingBehavior` records queries under `application.commands.*` and names their activity `Command {Name}`.
- `application.outbox.events_saved` is incremented twice per event, by `EventBusOutboxService` and `OutboxMessageService`.

See [Telemetry](../architecture/06-telemetry.md#known-gaps).

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
| Failed `Result` is committed | `TransactionBehavior` rolls back when the command returns a failed `Result`; `IdempotencyBehavior` calls `IIdempotencyService.DiscardAsync` (deletes the Redis key, detaches the `IdempotencyEntry`), so the same `x-requestid` can be retried | 2026-10-13 |
| Redis `processing` key survives failures | `IdempotencyBehavior` also calls `DiscardAsync` (best effort, original exception rethrown) when the command throws, so a retry within `ProcessingTtl` is no longer answered as a duplicate | 2026-10-15 |
