# Épica 2: Close Subscription Lifecycle — Closure Summary

## Overview

Épica 2 implements the complete subscription lifecycle from customer onboarding through activation and go-live. The flow is orchestrated through two separate DDD aggregates communicating via domain events and integration events through a transactional outbox.

## Delivered Components

### Domain Model

| Aggregate | Steps | Statuses | Domain Events |
|---|---|---|---|
| `OnboardingProcess` | 7 (None → SubscriptionCreated → TenantCreated → OwnerCompanyCreated → AdminProvisioningRequested → AdminUserProvisioned → AdminMembershipCreated → Completed) | Draft, InProgress, WaitingForExternalProvisioning, Completed, Failed | SubscriptionCreated, TenantCreated, CompanyCreated, AdminProvisioningRequested |
| `CustomerActivationProcess` | 5 (None → ActivationRequested → ActivationDelivered → UserActivated → CustomerReady → Completed) | Draft, InProgress, WaitingForNotificationDelivery, WaitingForUserActivation, Completed, Failed | CustomerActivationProcessStarted, ActivationEmailRequested, CustomerAdminActivated, CustomerGoLive |

### Commands (8)

- `StartCustomerOnboardingCommand` — Initiates the full onboarding flow
- `RetryOnboardingProcessCommand` — Retries a failed onboarding
- `CompleteOnboardingCommand` — Marks onboarding as complete after external provisioning
- `StartCustomerActivationProcessCommand` — Creates and starts the activation flow
- `RetryCustomerActivationProcessCommand` — Retries a failed activation
- `ResendActivationEmailCommand` — Re-sends the activation email
- `ConfirmActivationDeliveryCommand` — Marks email as delivered
- `CompleteActivationOnUserActivatedCommand` — Completes activation after user confirms

### Domain Event Handlers (5)

- `TenantCreatedDomainEventHandler` → publishes `TenantCreatedIntegrationEvent`
- `CompanyCreatedDomainEventHandler` → publishes `CompanyCreatedIntegrationEvent`
- `AdminProvisioningRequestedDomainEventHandler` → publishes `CustomerAdminProvisioningRequestedIntegrationEvent`
- `ActivationEmailRequestedDomainEventHandler` → publishes `ActivationEmailRequestedIntegrationEvent`
- `CustomerGoLiveDomainEventHandler` → publishes `CustomerActivationCompletedIntegrationEvent` + `CustomerGoLiveIntegrationEvent`

### Integration Event Handlers (5)

| Service | Handler | Reacts To |
|---|---|---|
| Identity | `TenantCreatedIntegrationEventHandler` | Creates local tenant record |
| Identity | `CustomerAdminProvisioningRequestedIntegrationEventHandler` | Provisions external admin user |
| Accounts | `ExternalUserProvisionedIntegrationEventHandler` | Completes onboarding, starts activation |
| Accounts | `ActivationDeliveredIntegrationEventHandler` | Confirms email delivery |
| Accounts | `UserActivatedIntegrationEventHandler` | Completes activation on user confirmation |
| Notifications | `ActivationEmailRequestedIntegrationEventHandler` | Sends activation email |
| Notifications | `CompanyCreatedIntegrationEventHandler` | Sends welcome emails |

### APIs

- **OnboardingApi** (5 endpoints): POST start, GET list, GET detail, GET result, POST retry
- **ActivationApi** (4 endpoints): GET list, GET detail, POST retry, POST resend

### BackOffice UI

- Onboarding: List + Detail pages with polling and retry actions
- Activation: List + Detail pages with polling, retry, and resend actions

### Unit Tests: 293 passing

| Category | Test Files | Tests |
|---|---|---|
| Domain (Onboarding) | 3 files | ~30 tests |
| Domain (Activation) | 1 file | ~25 tests |
| Domain (Supporting) | 4 files (Company, Tenant, Subscription, UserMembership) | ~40 tests |
| Command Handlers | 8 files | ~45 tests |
| Domain Event Handlers | 5 files | 11 tests |
| Integration Event Handlers | 5 files | ~15 tests |
| Queries | 1 file | ~20 tests |
| Identity | 4 files | ~25 tests |
| Notifications | 3 files | ~10 tests |

## Architectural Decisions Made

- **ADR-002**: Separate BackOffice and Manager apps (Razor Class Library for shared code)
- **ADR-003**: SignalR + Watchdog over Processor pattern for real-time notifications

## Key Patterns Established

1. **Domain events → Integration events** (never from command handlers)
2. **Transactional outbox** for reliable messaging
3. **Idempotent commands** with `IdempotentCommand<T>` base class
4. **Polling-based UI monitoring** (interim solution, SignalR planned for P2)

## Notes

- `CustomerActivationProcessStartedDomainEvent` has no handler by design — it's a tracking/auditing event for future observability use (audit log, telemetry enrichment)
- The handoff between aggregates occurs via `ExternalUserProvisionedIntegrationEventHandler` which completes onboarding AND starts activation in a single transaction

## Deferred to Future Épicas

| Item | Priority | Target |
|---|---|---|
| SignalR Hub (replace polling) | P2 | Épica 3 |
| Watchdog services (timeout detection) | P3 | Épica 4+ |
| QltManagerApp (customer-facing) | P2 | Épica 3 |
| E2E integration tests | P2 | Épica 3 sprint 1 |
