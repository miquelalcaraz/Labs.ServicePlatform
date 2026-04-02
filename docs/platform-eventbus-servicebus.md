# Platform.EventBus.ServiceBus — Executive Summary

## Overview

`Platform.EventBus.ServiceBus` is the Azure Service Bus implementation of the `IEventBus` abstraction. It enables asynchronous, loosely-coupled communication between microservices using the **topic/subscription** messaging pattern.

Each service publishes integration events to a shared topic (`qlt-event-bus`) and receives only the events it subscribes to, filtered by **correlation rules** on the subscription.

### Package Dependencies

| Package | Purpose |
|---|---|
| `Aspire.Azure.Messaging.ServiceBus` | Aspire-native Service Bus client registration |
| `AspNetCore.HealthChecks.AzureServiceBus` | Liveness/readiness probes for the Service Bus connection |
| `Platform.EventBus` | `IEventBus` abstraction and `EventBusSubscriptionInfo` |

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
                   │             │       │             │       │               │
                   │ Rules:      │       │ Rules:      │       │ Rules:        │
                   │ - OrderPaid │       │ - UserCreated│      │ - OrderPaid   │
                   │             │       │             │       │ - UserCreated │
                   └──────┬──────┘       └──────┬──────┘       └───────┬───────┘
                          │                     │                      │
                   ┌──────▼──────┐       ┌──────▼──────┐       ┌──────▼────────┐
                   │  Service B  │       │  Service C  │       │  Service D    │
                   │ (Consumer)  │       │ (Consumer)  │       │ (Consumer)    │
                   └─────────────┘       └─────────────┘       └───────────────┘
```

Each subscription uses **CorrelationRuleFilter** on the `Subject` property to receive only relevant event types. Rules are synchronized automatically on startup.

---

## Key Design Decisions

| Decision | Rationale |
|---|---|
| `AutoCompleteMessages = false` | Explicit control: complete on success, abandon on failure, dead-letter on unknown types |
| `IHostedService` lifecycle | Processor starts/stops with the host. `TaskCompletionSource` gates `PublishAsync` until ready |
| Normalized event type names | Strips `IntegrationEvent` suffix (e.g., `OrderPaidIntegrationEvent` → `OrderPaid`) for cleaner routing keys |
| Immutable local dictionary | `NormalizeEventTypeNames()` builds a private copy — never mutates the shared `IOptions<EventBusSubscriptionInfo>` singleton |
| `MessageId = @event.Id` | Enables Service Bus duplicate detection when configured on the topic |
| OpenTelemetry tracing | `traceparent`/`tracestate` propagated via `ApplicationProperties` for end-to-end distributed traces |

---

## Message Lifecycle

### Publish Flow

```
PublishAsync(@event)
  │
  ├─ Wait for StartAsync to complete (30s timeout)
  ├─ Start OTel Activity (Producer)
  ├─ Serialize event to UTF-8 JSON
  ├─ Set MessageId, Subject (routing key), traceparent
  ├─ SendMessageAsync(message, cancellationToken)
  ├─ Record success/failure metric
  └─ Return / throw
```

### Receive Flow

```
OnMessageReceived(eventArgs)
  │
  ├─ ProcessEvent(eventName, body, receivedMessage)
  │   ├─ Extract traceparent → start OTel Activity (Consumer)
  │   ├─ Lookup event type in _normalizedEventTypes
  │   │   ├─ NOT FOUND → return false
  │   │   └─ FOUND → deserialize → resolve handlers → invoke each
  │   └─ return true
  │
  ├─ if processed == true  → CompleteMessageAsync()
  ├─ if processed == false → DeadLetterMessageAsync("UnknownEventType")
  └─ if exception thrown   → AbandonMessageAsync() → retry via Service Bus
```

### Retry & Dead-Letter Semantics

| Scenario | Action | Result |
|---|---|---|
| Handler succeeds | `CompleteMessageAsync` | Message removed from subscription |
| Handler throws (transient) | `AbandonMessageAsync` | Message immediately available for retry |
| Max retries exceeded | Automatic (Service Bus) | Message moved to DLQ after `MaxDeliveryCount` |
| Unknown event type | `DeadLetterMessageAsync` | Message moved to DLQ with reason `"UnknownEventType"` |

---

## Configuration

### DI Registration

```csharp
// In service's Program.cs or HostApplicationBuilder setup
builder.AddServiceBusEventBus("messaging");
```

This registers:
- `ServiceBusClient` via Aspire's `AddAzureServiceBusClient`
- `IEventBus` → `ServiceBusEventBus` (singleton)
- `IHostedService` → same `ServiceBusEventBus` instance

### appsettings.json

```json
{
  "EventBus": {
    "SubscriptionClientName": "Accounts"
  }
}
```

| Property | Required | Description |
|---|---|---|
| `SubscriptionClientName` | Yes | Azure Service Bus subscription name for this service |

### Event Type Registration

```csharp
builder.AddServiceBusEventBus("messaging")
    .AddSubscription<OrderPaidIntegrationEvent, OrderPaidIntegrationEventHandler>()
    .AddSubscription<UserCreatedIntegrationEvent, UserCreatedIntegrationEventHandler>();
```

Registered event types are stored in `EventBusSubscriptionInfo.EventTypes` and used for:
1. Creating correlation filter rules on the subscription
2. Deserializing incoming messages to the correct CLR type
3. Resolving `IIntegrationEventHandler<T>` from DI

---

## Error Handling

| Layer | Strategy |
|---|---|
| **Publish** | Azure SDK built-in retry (exponential backoff). On final failure, exception propagates to caller. |
| **Receive — transient failure** | `AbandonMessageAsync` → message returns to subscription immediately for retry. After `MaxDeliveryCount` (default: 10), Service Bus auto-moves to DLQ. |
| **Receive — unknown event** | `DeadLetterMessageAsync` with reason `"UnknownEventType"` — no retry, preserved for investigation. |
| **Receive — abandon failure** | Logged at `Error` level. Message lock expires naturally → Service Bus retries. |
| **Startup failure** | `TaskCompletionSource` set to faulted → all `PublishAsync` calls fail fast with the startup exception. |
| **Shutdown** | `StopProcessingAsync(cancellationToken)` drains in-flight messages before disposing resources. 10-second timeout on `DisposeAsync`. |

### PII-Safe Logging

Message bodies are **never logged**. Error logs reference `MessageId` and `EventName` only.

---

## Observability

### Distributed Tracing (OpenTelemetry)

| Activity | Kind | Tags |
|---|---|---|
| `Publish {EventName}` | Producer | `messaging.system`, `messaging.destination.name`, `event.type`, `event.id`, `messaging.success` |
| `Process {EventName}` | Consumer | `messaging.system`, `messaging.source.name`, `event.type`, `event.id`, `event.resolved`, `event.handler_count`, `messaging.success` |

Trace context is propagated via `ApplicationProperties["traceparent"]` for cross-service correlation.

### Metrics (Counters)

| Metric | Tags | Description |
|---|---|---|
| `IntegrationEventsPublished` | `event_type`, `success` | Total events published to Service Bus |
| `IntegrationEventsReceived` | `event_type`, `success` | Total events processed from subscription |

### Structured Log Events

| Level | Event |
|---|---|
| `Information` | `Successfully published event {EventType} with ID {EventId}` |
| `Information` | `Successfully processed event {EventType} with {HandlerCount} handler(s)` |
| `Warning` | `Unable to resolve event type for event name {EventName}` |
| `Error` | `Error processing message {MessageId} for event {EventName}` |
| `Error` | `Failed to publish event to ServiceBus: {EventId}` |

---

## Deployment Considerations

| Item | Recommendation |
|---|---|
| **MaxDeliveryCount** | Default is 10. Adjust based on handler failure patterns. |
| **DLQ Monitoring** | Configure Azure Monitor alert on `DeadLetterMessageCount > 0` per subscription. |
| **Health Checks** | Register `AspNetCore.HealthChecks.AzureServiceBus` for orchestrator probes (K8s, Container Apps). |
| **Topic Name** | Currently hardcoded as `qlt-event-bus`. Sufficient for single-environment-per-namespace. Make configurable if sharing namespaces across environments. |
| **Duplicate Detection** | Enable on the topic if at-most-once delivery is required. `MessageId` is already set to `@event.Id`. |
| **Subscription Rules** | Synchronized idempotently on every startup. Safe for multi-instance deployments. |

---

## Project Structure

```
Platform.EventBus.ServiceBus/
├── Builders/
│   └── ServiceBusExtensions.cs        # IHostApplicationBuilder DI registration
├── Options/
│   └── EventBusOptions.cs             # SubscriptionClientName configuration
└── ServiceBusEventBus.cs              # Core implementation (IEventBus + IHostedService)
```
