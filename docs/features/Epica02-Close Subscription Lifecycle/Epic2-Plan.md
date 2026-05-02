## Plan: Epic2 Close Subscription Lifecycle End-to-End

Implement Epic2 as a separate activation lifecycle (not reopening onboarding), delivered in two increments: MVP for operational flow and hardening for resilience/observability. Reuse Epic1 patterns already implemented in Accounts, Identity, and Notifications (idempotent commands, integration handlers, outbox, retry semantics, backoffice APIs).

**Steps**
1. Phase 0 - Baseline and contract alignment
1. Validate naming and event contract boundaries across services before coding: ActivationEmailRequestedIntegrationEvent, ActivationDeliveredIntegrationEvent, UserActivatedIntegrationEvent, CustomerGoLiveIntegrationEvent.
1. Confirm final ownership per service: Accounts orchestrates process state, Notifications sends activation mail and publishes delivery, Identity emits user activation.
1. Freeze V1 scope decisions: CustomerReady auto-transition after user activation; no first-login wizard in MVP.
1. Phase 1 - Accounts domain and persistence foundation (blocks most other work)
1. Create new aggregate CustomerActivationProcess in Accounts Domain with dual state machine (Status + Step), retry, fail, and strict guards.
1. Add domain events for activation milestones and go-live trigger (internal events).
1. Create repository contract and implementation mirroring onboarding repository patterns.
1. Add EF configuration and DbSet entries in write/read contexts; generate migration for new activation table and indexes (SubscriptionId, TenantId, CorrelationId, AdminUserId).
1. Add unit tests for all valid/invalid transitions and retry behavior.
1. Phase 2 - Accounts application layer and API (depends on Phase 1)
1. Add idempotent commands and handlers: StartCustomerActivationProcess, RetryCustomerActivationProcess, ResendActivationEmail.
1. Implement command handler semantics to match existing platform patterns: persist aggregate, SaveChangesAsync, then AddAndSaveEventAsync via outbox when publishing integration events.
1. Add query models/handlers for activation detail/list views using read context.
1. Add backoffice activation endpoints (detail, retry, resend) reusing existing authorization and response patterns from onboarding API.
1. Add handler wiring in DI and event bus subscriptions for incoming activation events.
1. Phase 3 - Epic1 to Epic2 handoff in Accounts (depends on Phase 2)
1. Extend ExternalUserProvisionedIntegrationEventHandler so onboarding completion triggers activation start idempotently.
1. Keep existing duplicate guard behavior and correlation logging; add guard against creating duplicate activation process for same subscription/admin.
1. Add focused tests for automatic handoff and duplicate event replay.
1. Phase 4 - Notifications integration (parallel with Phase 5 once Phase 0 contracts are frozen)
1. Add ActivationEmailRequestedIntegrationEvent handler in Notifications.
1. Implement activation email composition and sending with preferredLanguage fallback policy.
1. Publish ActivationDeliveredIntegrationEvent through outbox only on successful send.
1. Add handler idempotency tests and failure path tests (send failure, duplicate event).
1. Register event subscription and event type assembly in notifications outbox configuration.
1. Phase 5 - Identity activation event integration (parallel with Phase 4)
1. Implement emission of UserActivatedIntegrationEvent when activation flow is completed in Identity.
1. Ensure event publishing uses outbox service and remains idempotent.
1. In Accounts, add UserActivatedIntegrationEvent handler to advance activation process: MarkUserActivated -> MarkCustomerReady -> Complete -> publish CustomerGoLiveIntegrationEvent.
1. Add user mismatch and invalid-state guard tests.
1. Phase 6 - End-to-end completion and API integration tests (depends on Phases 3, 4, 5)
1. Add functional tests for full flow: onboarding complete -> activation email requested -> delivery confirmed -> user activated -> go-live.
1. Add retry/resend functional tests and duplicate request replay tests (same x-requestid returns success semantics).
1. Add authorization tests for activation endpoints based on existing onboarding policy style.
1. Phase 7 - Hardening increment (post-MVP)
1. Add observability: structured logs with CorrelationId in all new handlers and commands, plus activation funnel metrics.
1. Add operational safeguards: resend cooldown/max attempts and explicit activation error taxonomy.
1. Add timeout/recovery operational behavior for prolonged waiting states and dead-letter visibility.
1. Complete cross-service regression pass and performance sanity checks.

**Dependencies and parallelism**
1. Sequential chain: Phase 1 -> Phase 2 -> Phase 3.
1. Parallel after contract freeze: Phase 4 and Phase 5.
1. Sequential completion: Phase 6 after 3/4/5.
1. Hardening: Phase 7 after MVP validation.

**Relevant files**
- d:/Projects/QltSystem2.0/QltSystem/docs/features/Epica02-Close Subscription Lifecycle/00.Definicion.md - Epic2 business objective and lifecycle boundary.
- d:/Projects/QltSystem2.0/QltSystem/docs/features/Epica02-Close Subscription Lifecycle/01.Dominio.md - target state machine/invariants/events.
- d:/Projects/QltSystem2.0/QltSystem/docs/features/Epica02-Close Subscription Lifecycle/02.User Stories.md - user-story scope and acceptance criteria.
- d:/Projects/QltSystem2.0/QltSystem/docs/features/Epica02-Close Subscription Lifecycle/03.Implementation guide.md - command and aggregate scaffolding guidance.
- d:/Projects/QltSystem2.0/QltSystem/.github/PROJECT-CONTEXT.md - non-breaking evolution and architecture guardrails.
- d:/Projects/QltSystem2.0/QltSystem/.github/copilot-instructions.md - layering constraints, idempotency semantics, and forbidden patterns.
- d:/Projects/QltSystem2.0/QltSystem/src/Qlt.Services/Qlt.Services.Accounts/Domain/Models/OnboardingAggregate/OnboardingProcess.cs - template for state machine + retry model.
- d:/Projects/QltSystem2.0/QltSystem/src/Qlt.Services/Qlt.Services.Accounts/Application/Commands/RetryOnboardingProcessCommandHandler.cs - template for retry and outbox republish.
- d:/Projects/QltSystem2.0/QltSystem/src/Qlt.Services/Qlt.Services.Accounts/Infrastructure/IntegrationEvents/EventHandling/ExternalUserProvisionedIntegrationEventHandler.cs - Epic1 completion hook to extend for activation start.
- d:/Projects/QltSystem2.0/QltSystem/src/Qlt.Services/Qlt.Services.Accounts/Infrastructure/Persistence/AccountContext.cs - add activation DbSet.
- d:/Projects/QltSystem2.0/QltSystem/src/Qlt.Services/Qlt.Services.Accounts/Infrastructure/Persistence/AccountReadContext.cs - add activation read DbSet.
- d:/Projects/QltSystem2.0/QltSystem/src/Qlt.Services/Qlt.Services.Accounts/Infrastructure/Extensions/Extensions.cs - register repo and event subscriptions.
- d:/Projects/QltSystem2.0/QltSystem/src/Qlt.Services/Qlt.Services.Accounts/Api/Api/BackOfficeOnboardingApi.cs - API style template for activation endpoints.
- d:/Projects/QltSystem2.0/QltSystem/src/Qlt.Infrastructure/Qlt.Infrastructure.Identity/IntegrationEvents/EventHandling/CustomerAdminProvisioningRequestedIntegrationEventHandler.cs - outbox publishing template in Identity.
- d:/Projects/QltSystem2.0/QltSystem/src/Qlt.Infrastructure/Qlt.Infrastructure.Identity/Extensions/Extensions.cs - identity event subscriptions and wiring.
- d:/Projects/QltSystem2.0/QltSystem/src/Qlt.Services/Qlt.Services.Notifications/IntegrationEvents/EventHandling/CompanyCreatedIntegrationEventHandler.cs - notifications integration handler style.
- d:/Projects/QltSystem2.0/QltSystem/src/Qlt.Services/Qlt.Services.Notifications/Extensions/Extensions.cs - notifications event bus and outbox wiring.
- d:/Projects/QltSystem2.0/QltSystem/tests/Services/Services.UnitTests/Accounts/Domain/OnboardingProcessTests.cs - domain test template for state machine.
- d:/Projects/QltSystem2.0/QltSystem/tests/Services/Services.UnitTests/Accounts/Application/Commands/RetryOnboardingProcessCommandHandlerTests.cs - command handler test template.
- d:/Projects/QltSystem2.0/QltSystem/tests/Services/Services.UnitTests/Accounts/Infrastructure/ExternalUserProvisionedIntegrationEventHandlerTests.cs - integration-event handler unit test template.
- d:/Projects/QltSystem2.0/QltSystem/tests/Services/Services.FunctionalTests/Accounts/BackOfficeOnboardingApiTests.cs - functional API test template.

**Verification**
1. Build baseline: run workspace build task and ensure no compilation regressions.
1. Unit tests Accounts Domain/Application/Infrastructure: validate activation state machine, guards, idempotent command behavior.
1. Unit tests Notifications and Identity handlers: validate send/publish flow and outbox usage.
1. Functional tests Accounts APIs: activation detail/retry/resend endpoints, auth policies, idempotent duplicate semantics.
1. Integration flow test: onboarding completion triggers activation process and cross-service events conclude in CustomerGoLive.
1. Regression checks: existing onboarding tests continue passing after handoff extension.

**Decisions**
- Included scope: complete end-to-end implementation across Accounts + Identity + Notifications + backoffice activation API surface.
- Delivery strategy: two increments (MVP first, hardening second).
- Modeling decision: new CustomerActivationProcess aggregate, separate from OnboardingProcess.
- V1 simplification: auto CustomerReady after UserActivated; no first-login UI wizard in MVP.

**Explicitly excluded from MVP**
- Customer-facing first-login wizard UX.
- Advanced policy engine for TenantReadiness beyond deterministic V1 checks.
- Billing/usage downstream consumers beyond emitting CustomerGoLiveIntegrationEvent.

**Further Considerations**
1. Define whether resend cooldown is strict business rule in MVP or only in hardening.
2. Decide if activation list endpoint is mandatory in MVP or can be deferred after detail endpoint.
3. Decide if go-live should immediately activate subscription aggregate state or stay as event-only transition in MVP.
