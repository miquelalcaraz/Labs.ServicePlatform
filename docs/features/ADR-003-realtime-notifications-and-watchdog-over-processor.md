# ADR-003: Real-time Notifications (SignalR) + Watchdog over Processor Pattern

**Status:** Accepted  
**Date:** 2025-07-01  
**Context:** Épica 2 — Close Subscription Lifecycle (Onboarding + Activation monitoring)

---

## Context

The BackOffice UI currently uses **polling** to track Onboarding and Activation process state changes (see `Detail.razor.cs` in both Onboarding and Activation pages). This works but is inefficient and provides delayed feedback.

Two approaches were evaluated:

1. **Processor pattern** (à la eShop `OrderProcessor`/`PaymentProcessor`) — a background service that orchestrates the workflow and could push state changes.
2. **SignalR push + Watchdog** — keep existing event-driven orchestration, add real-time notifications via SignalR Hub, and a future watchdog for timeout detection.

---

## Decision

**Use SignalR for real-time push notifications to the frontend. Add a Watchdog background service for timeout/stuck-process detection. Do NOT adopt the Processor pattern for orchestration.**

---

## Rationale: Why NOT a Processor

### The Processor does not reduce complexity

The system already has a fully functional event-driven orchestration:

```
Command → Aggregate → DomainEvent → Handler → IntegrationEvent (outbox)
IntegrationEvent (response) → Handler → Aggregate transition
```

A Processor would replicate this logic in a centralized switch/poll loop:

```csharp
// What a Processor would look like — a large switch duplicating handler logic
switch (process.CurrentStep)
{
    case AdminProvisioningRequested: await CallIdentityAsync(); break;
    case AdminUserProvisioned: await CreateMembershipAsync(); break;
    case AdminMembershipCreated: await StartActivationAsync(); break;
    case ActivationRequested: await SendEmailAsync(); break;
    // ... 10+ cases
}
```

| Concern | Event-driven (current) | Processor |
|---------|----------------------|-----------|
| Orchestration | Distributed across small, focused handlers | Concentrated in one large class |
| Testability | Each handler tested in isolation | Single class with many branches |
| Integration events | Still needed for cross-service communication | Still needed — Processor doesn't eliminate them |
| Domain event count | Same | Same (or hidden inside Processor) |
| Command count | Same | Same (Processor still needs to trigger actions) |
| Traceability | Each event/handler is an explicit, auditable step | Steps hidden inside Processor internals |

### The Processor does not enable push notifications

Push notifications are a **transport concern**, orthogonal to orchestration:

- SignalR works with event-driven handlers (notify on each state change)
- SignalR works with Processors too (notify after each step)
- The enabling factor is the **SignalR Hub**, not the orchestration style

### When a Processor WOULD make sense

- If there were no domain events and the system relied purely on polling DB state
- If the workflow had complex conditional branching that doesn't map to linear state machines
- If integration with external systems required centralized retry/compensation logic (saga orchestrator)

None of these apply to our Onboarding/Activation flow.

---

## Architecture

```
┌─ Event-driven orchestration (already implemented) ────────────────┐
│                                                                    │
│  Command → Aggregate → DomainEvent → Handler → Outbox             │
│  IntegrationEvent (response) → Handler → Aggregate                │
│                                                                    │
│  ✅ No changes needed                                             │
└────────────────────────────────────────────────────────────────────┘

┌─ Real-time push (SignalR) ─────────────────────────────────────────┐
│                                                                    │
│  DomainEvent/IntegrationEvent Handler                              │
│       │                                                            │
│       ▼                                                            │
│  IHubContext<ProcessStatusHub>.Clients                             │
│       .Group("onboarding-{processId}")                             │
│       .SendAsync("StatusChanged", payload)                         │
│       │                                                            │
│       ▼                                                            │
│  Blazor component (HubConnection) → StateHasChanged()             │
│                                                                    │
│  Replaces: polling in Detail.razor.cs                              │
└────────────────────────────────────────────────────────────────────┘

┌─ Watchdog (future) ───────────────────────────────────────────────┐
│                                                                    │
│  Background service — periodic check (every 5-10 min):            │
│                                                                    │
│  • WaitingForExternalProvisioning > 30 min → Fail or auto-retry   │
│  • WaitingForNotificationDelivery > 15 min → Fail or auto-retry   │
│  • WaitingForUserActivation > 48h → Alert or expire               │
│                                                                    │
│  Actions: Fail() / Retry() / Publish alert integration event      │
│                                                                    │
│  Naming: OnboardingWatchdogService / ActivationWatchdogService    │
└────────────────────────────────────────────────────────────────────┘
```

---

## SignalR Implementation Approach

### Hub (in BFF or Accounts service)

```csharp
public class ProcessStatusHub : Hub
{
    public async Task SubscribeToProcess(string processType, Guid processId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"{processType}-{processId}");
    }

    public async Task UnsubscribeFromProcess(string processType, Guid processId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"{processType}-{processId}");
    }
}
```

### Notification from handlers

```csharp
// Inside any domain event handler or integration event handler that changes process state:
await _hubContext.Clients
    .Group($"activation-{processId}")
    .SendAsync("StatusChanged", new ProcessStatusUpdate
    {
        ProcessId = processId,
        Status = newStatus,
        CurrentStep = currentStep,
        Timestamp = DateTimeOffset.UtcNow
    }, cancellationToken);
```

### Blazor component (replaces polling)

```csharp
protected override async Task OnInitializedAsync()
{
    await LoadDetailAsync();

    hubConnection = new HubConnectionBuilder()
        .WithUrl(NavigationManager.ToAbsoluteUri("/hubs/process-status"))
        .WithAutomaticReconnect()
        .Build();

    hubConnection.On<ProcessStatusUpdate>("StatusChanged", async update =>
    {
        detail = await RefreshFromUpdate(update);
        await InvokeAsync(StateHasChanged);
    });

    await hubConnection.StartAsync();
    await hubConnection.InvokeAsync("SubscribeToProcess", "activation", ActivationProcessId);
}
```

---

## Watchdog Design (future implementation)

| Parameter | Onboarding | Activation |
|-----------|-----------|------------|
| Timeout: WaitingForExternal | 30 min | N/A |
| Timeout: WaitingForNotification | N/A | 15 min |
| Timeout: WaitingForUserActivation | N/A | 48h |
| Max auto-retries | 3 | 3 |
| Action on max retries exceeded | Fail + alert | Fail + alert |
| Check interval | 5 min | 5 min |

---

## Consequences

1. **Immediate:** Keep current polling as-is until SignalR is implemented (functional, just suboptimal)
2. **Next step:** Implement `ProcessStatusHub` + notify from existing handlers → replace polling in Detail pages
3. **Future:** Implement `OnboardingWatchdogService` and `ActivationWatchdogService` for timeout detection
4. **Never:** Do not introduce a Processor/Orchestrator pattern — the event-driven flow is the single source of truth for state transitions

---

## Implementation Priority

| Priority | Item | Blocks |
|----------|------|--------|
| P2 | SignalR Hub + frontend integration | Nothing (polling works as fallback) |
| P3 | Watchdog services | Nothing (manual retry via UI works) |

Both items can be implemented after Épica 2 E2E is closed.
