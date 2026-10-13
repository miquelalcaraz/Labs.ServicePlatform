# Sample Feature: Customer Onboarding & Activation

The business services in this repository (Accounts, Identity, Notifications and the BackOffice app) implement **one sample feature** whose purpose is to show the building blocks working together across services. It is a teaching example, not a product: the documents below describe only what is implemented.

| Document | Description |
|----------|-------------|
| [Sample Domain](01-sample-domain.md) | Bounded contexts, glossary, aggregates, process state machines and domain events |
| [Onboarding & Activation Workflow](02-onboarding-activation-workflow.md) | End-to-end flow across services, integration events, idempotency, failures and retries, design decisions |
| [BackOffice API & UI](03-backoffice-api-ui.md) | Accounts API endpoints, read models, authorization matrix, BackOffice pages, how to try it locally |

## Building Blocks Showcased

| Building block | Where to see it in the feature |
|---|---|
| Idempotent commands (`IdempotentCommand<T>`, `x-requestid`) | `StartCustomerOnboardingCommand`, retry/resend commands |
| Single-transaction pipeline + domain events | Onboarding start creates four aggregates and dispatches their events in one transaction |
| Transactional outbox | Every integration event is written by a domain event handler (Accounts) or command handler (Identity, Notifications) |
| Idempotent consumers | All integration event handlers, plus business-level checks in each command |
| Process aggregates / state machines | `OnboardingProcess`, `CustomerActivationProcess` |
| Event bus (Azure Service Bus) | Choreography between Accounts, Identity and Notifications |
| CQRS read model | Paged lists, detail timelines and result views over `AccountReadContext` |
| Telemetry | Distributed traces per step, outbox/idempotency metrics, `accounts.activation.processes.*` counters |
| BFF + role-based authorization | BackOffice → BFF → Accounts API with policy per endpoint |

For the platform internals see [Architecture](../architecture/README.md); for how-to guides see [Developer Guide](../developer-guide/README.md).
