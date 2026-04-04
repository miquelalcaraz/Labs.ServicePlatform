---

## Appendix A — MassTransit Comparison

This section documents the evaluation of MassTransit as a potential replacement for the custom `ServiceBusEventBus` implementation. The analysis was performed at the time of production-readiness review.

### Feature-by-Feature Comparison

| Capability | Custom Implementation | MassTransit |
|---|---|---|
| **Publish/Subscribe** | ✅ `ServiceBusEventBus` + topic/subscription | ✅ Built-in |
| **Topology management** | ✅ `ConfigureSubscriptionRulesAsync` (idempotent) | ✅ Auto-configuration |
| **Transactional Outbox** | ✅ `OutboxProcessor` + `EventBusOutboxService` | ✅ EF Core Outbox |
| **Idempotent Consumer** | ✅ `IdempotencyIntegrationEventEfDecorator` | ✅ `InMemoryOutbox` / dedup |
| **Telemetry (OTel)** | ✅ Manual activities + metrics + trace propagation | ✅ Built-in (auto-instrumented) |
| **Decorator pipeline** | ✅ Telemetry → Idempotency → Handler (Scrutor) | ✅ Middleware filters |
| **Retry policies** | ⚠️ Transport-level only (abandon → MaxDeliveryCount) | ✅ Immediate, interval, exponential per-consumer |
| **Scheduled redelivery** | ❌ Not implemented | ✅ Built-in (uses SB scheduled messages) |
| **Circuit breaker** | ❌ Not implemented | ✅ Built-in per-consumer |
| **Sagas / State machines** | ❌ Not implemented | ✅ Automatonymous state machines |
| **Request/Response** | ❌ Not needed (uses MediatR CQRS) | ✅ Built-in |
| **Test harness** | ✅ Integration tests with SB emulator | ✅ InMemory test harness |
| **Multi-transport** | ✅ `IEventBus` abstraction (SB + RabbitMQ) | ✅ Plug-and-play transports |
| **AOT / trimming** | ✅ `IsAotCompatible`, suppression attributes | ❌ NOT AOT-compatible |
| **Aspire-native** | ✅ `AddAzureServiceBusClient` integration | ⚠️ Partial (community contrib) |
| **Message envelope** | Lean (Subject + body + ApplicationProperties) | Opaque (MT headers, `__messageType`, etc.) |
| **Control over wire format** | ✅ Full control | ❌ MT-specific envelope |

### What MassTransit Would Add

Only **3 features** not currently covered by the custom implementation:

1. **Consumer-level retry policies** — Retry with exponential backoff *before* abandoning to the transport. Current flow: exception → abandon → Service Bus redelivery (immediate). MT adds `UseMessageRetry(r => r.Exponential(5, ...))` at the consumer level.
2. **Scheduled redelivery** — "Retry this message in 30 seconds." Useful for transient external dependencies. Achievable natively with `ServiceBusMessage.ScheduledEnqueueTime` if needed.
3. **Sagas / State machines** — Long-running workflows with persistent state. Only relevant for multi-step orchestration (e.g., order → payment → shipping → notification).

### What Would Be Lost

| Concern | Impact |
|---|---|
| **AOT compatibility** | 🔴 MassTransit relies heavily on reflection. `IsAotCompatible` and trimming support would break. |
| **Wire format control** | 🔴 MT wraps messages in its own envelope with `__messageType` headers. Breaks compatibility with existing messages in the topic. |
| **Aspire-native integration** | 🟡 Current setup is fully Aspire-native. MT's Aspire support is less mature. |
| **Migration cost** | 🔴 All `IIntegrationEventHandler<T>` implementations, outbox, idempotent consumer, telemetry decorators would need rewriting to MT's `IConsumer<T>` model. |
| **Conceptual overlap** | 🟡 MediatR (CQRS) + MassTransit (messaging) is common but adds two "pipeline" models to reason about. |
| **Dependency weight** | 🟡 MassTransit pulls ~15+ packages. Current EventBus stack is 2 packages + custom code. |

### Decision

**MassTransit was not adopted.** Rationale:

- The custom implementation already covers all critical production patterns: transactional outbox, idempotent consumers, decorator pipeline, OTel tracing, graceful shutdown, and production-hardened error handling.
- The feature gap is small (consumer-level retry, scheduled redelivery) and both can be added incrementally to the existing decorator pipeline without framework migration.
- Adopting MassTransit would sacrifice AOT compatibility, wire-format control, and Aspire-native integration — all of which are architectural priorities for this project.
- The migration cost (rewriting all handlers and infrastructure) is disproportionate to the incremental value gained.

### When to Reconsider

- **Sagas are needed** — If complex multi-step orchestration workflows become a requirement.
- **Starting a new system** — For greenfield projects without existing infrastructure or AOT requirements.
- **AOT is no longer required** — If trimming/AOT constraints are relaxed in the future.