# Event Bus

## Overview

The Event Bus provides asynchronous, loosely-coupled communication between microservices using integration events. Services depend on the `IEventBus` abstraction — the underlying transport (RabbitMQ, Azure Service Bus) is swappable without code changes in business services.

**Abstraction layer**: `Platform.EventBus`  
**Implementations**: `Platform.EventBus.RabbitMQ`, `Platform.EventBus.ServiceBus`

---

## Architecture

```
┌──────────────┐     PublishAsync()     ┌─────────────────────┐
│   Service A  │ ─────────────────────► │  Topic              │
│  (Producer)  │                        │  "qlt-event-bus"    │
└──────────────┘                        └────────┬────────────┘
                                                 │
                          ┌──────────────────────┼──────────────────────┐
                          │                      │                      │
                   ┌──────▼──────┐       ┌───────▼─────┐       ┌───────▼───────┐
                   │ Subscription│       │ Subscription│       │ Subscription  │
                   │ "Accounts"  │       │ "Identity"  │       │"Notifications"│
                   │ Rules:      │       │ Rules:      │       │ Rules:        │
                   │ - OrderPaid │       │ - UserCreated│      │ - OrderPaid   │
                   └──────┬──────┘       └──────┬──────┘       └───────┬───────┘
                          │                     │                      │
                   ┌──────▼──────┐       ┌──────▼──────┐       ┌──────▼────────┐
                   │  Service B  │       │  Service C  │       │  Service D    │
                   └─────────────┘       └─────────────┘       └───────────────┘
```

Each subscription uses **CorrelationRuleFilter** on the `Subject` property to receive only relevant event types. Rules are synchronized automatically on startup.

---

## IEventBus Abstraction

Defined in `Platform.EventBus`:

```csharp
public interface IEventBus
{
    Task PublishAsync(IntegrationEvent @event, CancellationToken cancellationToken = default);
}
```

Event type registration and handler subscription use the builder pattern:

```csharp
builder.AddServiceBusEventBus("messaging")
    .AddSubscription<OrderPaidIntegrationEvent, OrderPaidIntegrationEventHandler>()
    .AddSubscription<UserCreatedIntegrationEvent, UserCreatedIntegrationEventHandler>();
```

`AddSubscription` registers:
1. The event type in `EventBusSubscriptionInfo.EventTypes` (for deserialization and routing)
2. The handler as `IIntegrationEventHandler<T>` in DI (for resolution on receive)

---

## Azure Service Bus Implementation

### Key Design Decisions

| Decision | Rationale |
|---|---|
| `AutoCompleteMessages = false` | Explicit control: complete on success, abandon on failure, dead-letter on unknown types |
| `IHostedService` lifecycle | Processor starts/stops with the host. `TaskCompletionSource` gates `PublishAsync` until ready |
| Normalized event type names | Strips `IntegrationEvent` suffix (e.g., `OrderPaidIntegrationEvent` → `OrderPaid`) for cleaner routing keys |
| `MessageId = @event.Id` | Enables Service Bus duplicate detection when configured on the topic |
| OpenTelemetry tracing | `traceparent`/`tracestate` propagated via `ApplicationProperties` for end-to-end distributed traces |

### Message Lifecycle

**Publish flow**:
```
PublishAsync(@event)
  ├─ Wait for StartAsync to complete (timeout)
  ├─ Start OTel Activity (Producer)
  ├─ Serialize event to UTF-8 JSON
  ├─ Set MessageId, Subject (routing key), traceparent
  └─ SendMessageAsync
```

**Receive flow**:
```
OnMessageReceived(eventArgs)
  ├─ Extract traceparent → start OTel Activity (Consumer)
  ├─ Lookup event type in normalized dictionary
  │   ├─ NOT FOUND → DeadLetterMessageAsync("UnknownEventType")
  │   └─ FOUND → deserialize → resolve handlers → invoke each
  ├─ Success → CompleteMessageAsync
  └─ Exception → AbandonMessageAsync (retry via Service Bus)
```

### Retry & Dead-Letter Semantics

| Scenario | Action | Result |
|---|---|---|
| Handler succeeds | `CompleteMessageAsync` | Message removed |
| Handler throws (transient) | `AbandonMessageAsync` | Immediate retry |
| Max retries exceeded | Automatic (Service Bus) | Moved to DLQ after `MaxDeliveryCount` |
| Unknown event type | `DeadLetterMessageAsync` | Moved to DLQ with reason |

---

## Configuration

### DI Registration

```csharp
builder.AddServiceBusEventBus("messaging");
```

Registers `ServiceBusClient` via Aspire, `IEventBus` → `ServiceBusEventBus` (singleton), and `IHostedService` for lifecycle management.

### appsettings.json

```json
{
  "EventBus": {
    "SubscriptionClientName": "Accounts"
  }
}
```

---

## Swapping Implementations

Changing the event bus transport requires **only** a DI registration change:

```csharp
// Today (Azure Service Bus)
builder.AddServiceBusEventBus("messaging");

// Tomorrow (RabbitMQ)
builder.AddRabbitMqEventBus("messaging");
```

Business service code (handlers, event types) remains unchanged. This is the core architectural principle: **"Platform evolves, services sleep peacefully."**

---

## Validation Checklist

- [ ] Services depend on `IEventBus`, never on `ServiceBusEventBus` or `RabbitMqEventBus`
- [ ] All integration event types registered via `AddSubscription<TEvent, THandler>()`
- [ ] `SubscriptionClientName` configured per service in `appsettings.json`
- [ ] Idempotent consumers handle potential duplicate deliveries (see [04-idempotency.md](04-idempotency.md))
