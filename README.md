# Miqa · Building Blocks Reference Architecture

![.NET](https://img.shields.io/badge/.NET-10.0-512BD4?logo=dotnet)
![Aspire](https://img.shields.io/badge/Aspire-13.6-512BD4)
![Status](https://img.shields.io/badge/status-in%20progress-yellow)
[![License: MIT](https://img.shields.io/badge/license-MIT-green)](LICENSE)

A **reference architecture for event-driven microservices** on **.NET 10** and **Aspire**, organized around a set of reusable **building blocks**: CQRS pipeline, domain and integration events, transactional outbox, idempotent commands and consumers, read models and observability.

The building blocks are applied to a [sample B2B domain](docs/features/README.md) — **customer onboarding and activation** — with three services, an IdentityServer, a BFF and a Blazor back office.

**Start here:** [Overview](#-overview) · [Building blocks](#-building-blocks) · [Run locally](#-running-the-project) · [Documentation](#-documentation) · [Known limitations](#️-project-status-and-known-limitations)

> [!NOTE]
> **Continuation of [Labs.Aspire.uSLearn](https://github.com/miquelalcaraz/Labs.Aspire.uSLearn).**
> uSLearn rebuilt the patterns of [dotnet/eShop](https://github.com/dotnet/eShop) step by step (domain events, integration events, idempotency, resilient transactions, telemetry). This repository takes the next step: it **extracts those patterns into layered, reusable building blocks** and adds the stages uSLearn had planned — identity with Duende IdentityServer and a Blazor + Radzen front end.
>
> **Educational reference** — not intended for direct production use. See [known limitations](#️-project-status-and-known-limitations).

*Miqa is the name of this lab project; it is not a company or a product.*

---

## 🧭 Overview

The guiding principle is **"Platform evolves, services sleep peacefully"**: business services depend on abstractions, so infrastructure (event bus, persistence, caching, telemetry) can change underneath them with minimal impact.

```mermaid
flowchart LR
    BO[BackOfficeApp<br/>Blazor + Radzen] --> BFF[backoffice-bff<br/>YARP]

    subgraph Services
        Accounts[Accounts API]
        Identity[Identity<br/>Duende IdentityServer]
        Notifications[Notifications API]
    end

    subgraph Infrastructure
        SQL[("SQL Server<br/>MiqaSystemDb · IdentityDb")]
        Redis[(Redis)]
        Bus{{"Azure Service Bus<br/>topic miqa-event-bus"}}
    end

    BFF --> Accounts & Identity
    Services --> SQL & Redis
    Services <-. integration events .-> Bus
```

> Accounts and Notifications share `MiqaSystemDb` (one schema each); Identity uses `IdentityDb`.

- **Accounts API** — companies, tenants, subscriptions, the customer **onboarding** process and the customer **activation** process (DDD aggregates and process state machines).
- **Identity** — Duende IdentityServer with ASP.NET Core Identity; provisions tenant admin users and activates them.
- **Notifications API** — welcome and activation emails, driven by integration events.
- **backoffice-bff** — YARP reverse proxy; the single public entry point for the browser, including IdentityServer routes.
- **BackOfficeApp** — Blazor Web App with Radzen components for the back-office operators.

### Integration events between services

| Publisher | Integration event | Consumer |
|---|---|---|
| Accounts | `CompanyCreated` | Notifications |
| Accounts | `TenantCreated` | Identity |
| Accounts | `CustomerAdminProvisioningRequested` | Identity |
| Accounts | `ActivationEmailRequested` | Notifications |
| Accounts | `CustomerGoLive`, `CustomerActivationCompleted` | — *(published for future consumers)* |
| Identity | `ExternalUserProvisioned` | Accounts |
| Identity | `UserActivated` | Accounts |
| Notifications | `ActivationDelivered` | Accounts |

Every event is written to the publisher's **transactional outbox** in the same database transaction as the business change, published after commit, and processed by **idempotent consumers**.

---

## 🧱 Building Blocks

| Project | Responsibility |
|---|---|
| `Domain.Core` | DDD seed work: `Entity` (domain events, `CreatedAt`/`UpdatedAt`, `RowVersion`), `ValueObject`, `IAggregateRoot`, `IRepository`, `IUnitOfWork`, `IDomainEvent` |
| `Domain.Application` | Cross-cutting MediatR behaviors: `LoggingBehavior`, `ValidationBehavior` (FluentValidation) |
| `Platform.Core` | `Result` / `Result<T>` and `Error` (railway-oriented results) |
| `Platform.Messaging.Abstractions` | Contracts: `ICommand<T>`, `IQuery<T>`, `IDomainEventHandler<T>`, `IIntegrationEventHandler<T>`, `IIdempotencyService`, `IEventBusOutboxService`, `IntegrationEvent` |
| `Platform.Messaging` | Persistence-ignorant logic: `IdempotencyBehavior`, telemetry behaviors/decorators, `IdempotentCommand<T>`, outbox/idempotency POCOs |
| `Platform.Messaging.EntityFramework` | `AddMiqaMediator`, `TransactionBehavior`, `DomainEventProcessorBehavior`, `IntegrationEventProcessorBehavior`, EF idempotency, outbox services and `OutboxProcessor` |
| `Platform.Messaging.Redis` | Redis fast path for idempotency checks |
| `Platform.Persistence.EntityFramework` | EF Core abstractions: `ITransactionalUnitOfWork`, `IDomainEntityChangeTracker`, fluent context configuration |
| `Platform.Persistence.EntityFramework.Relational` | `MiqaDBContext` (write) and `MiqaReadDbContext` (read-only), entity configurations, `Repository<T>`, `ResilientTransaction`, migrations |
| `Platform.EventBus` | `IEventBus` abstraction and subscription builder |
| `Platform.EventBus.ServiceBus` | Azure Service Bus implementation (default) |
| `Platform.EventBus.RabbitMQ` | RabbitMQ implementation (alternative, see limitations) |
| `Platform.Telemetry` | OpenTelemetry activity sources, meters and helpers |
| `Platform.ServiceDefaults` / `Platform.WebApi` / `Platform.Infrastructure` | Aspire service defaults, JWT authentication, OpenAPI + Scalar, `Result` → HTTP mapping, exception middleware, `IUserHttpContext` |

Every command flows through the same pipeline, configured with a single call:

```csharp
services.AddMiqaMediator(cfg =>
{
    cfg.RegisterServicesFromAssembly(AssemblyReference.Assembly);
    cfg.WithLogging(typeof(LoggingBehavior<,>));
    cfg.WithValidation(typeof(ValidationBehavior<,>));
});
```

```mermaid
flowchart LR
    L[Logging] --> I[Idempotency]
    I --> IE["IntegrationEventProcessor<br/><i>publishes outbox after commit</i>"]
    IE --> T["Transaction<br/><i>BEGIN / COMMIT</i>"]
    T --> V[Validation]
    V --> D["DomainEventProcessor<br/><i>dispatches domain events inside the transaction</i>"]
    D --> H[[Handler]]
```

Telemetry behaviors, the domain event publisher and the integration event decorators are registered by the same call. See [Command pipeline](docs/architecture/02-command-pipeline.md) for the details.

---

## 🏗️ Tech Stack

| Area | Technologies |
| --- | --- |
| Runtime | .NET 10, ASP.NET Core minimal APIs, API versioning, OpenAPI + Scalar |
| Orchestration | Aspire 13.6 (AppHost, service defaults, service discovery, health checks, dashboard) |
| Domain / application | DDD, CQRS with MediatR, pipeline behaviors, FluentValidation, Result pattern |
| Persistence | Entity Framework Core 10 + SQL Server, separate read context, Redis |
| Messaging | Azure Service Bus (emulator in development) or RabbitMQ, transactional outbox, idempotent consumers |
| Identity | Duende IdentityServer 8 + ASP.NET Core Identity, BFF with YARP |
| Front end | Blazor Web App + Radzen |
| Observability | Structured logging, OpenTelemetry traces and metrics (trace context propagated through the bus) |
| Testing | xUnit, Moq, FluentAssertions, EF InMemory, Testcontainers, Aspire hosting (in-process test fixtures), xUnit v3 for functional tests |

---

## 🧩 Solution Structure

```
src/
 ├── Miqa.Domain/            → Domain.Core, Domain.Application (DDD seed work and behaviors)
 ├── Miqa.Platform/          → Platform.* building blocks (messaging, persistence, event bus, telemetry, web)
 ├── Miqa.Contracts/         → Integration event contracts shared between services
 ├── Miqa.Services/
 │    ├── Accounts/         → Api, Application, Domain, Infrastructure
 │    └── Notifications/    → Api, Application, Domain, Infrastructure
 ├── Miqa.Infrastructure/
 │    ├── AppHost           → Aspire orchestration
 │    ├── Identity          → Duende IdentityServer
 │    └── Bff.BackOffice    → YARP backend for frontend
 └── Miqa.WebApps/
      └── Miqa.BackOfficeApp → Blazor + Radzen back office
tests/
 ├── Platform/              → UnitTests, IntegrationTests, FunctionalTests (+ TestWebApi host)
 └── Services/              → UnitTests, FunctionalTests
docs/                       → Architecture and developer documentation
```

---

## 🚀 Running the Project

### Requirements

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [Docker Desktop](https://www.docker.com/products/docker-desktop/) (or Podman) — **required**: Aspire runs SQL Server, Redis and the Service Bus emulator as containers

### Run with Aspire

```bash
git clone https://github.com/miquelalcaraz/Labs.CleanArchitecture.BuildingBlocks.git
cd Labs.CleanArchitecture.BuildingBlocks
dotnet build MiqaSystem.slnx
dotnet run --project src/Miqa.Infrastructure/Miqa.Infrastructure.AppHost
```

The AppHost starts the containers (SQL Server, Redis, Azure Service Bus emulator), applies the EF Core migrations, seeds demo data and launches `identity`, `accounts`, `notifications`, `backoffice-bff` and `BackOfficeApp`. The **Aspire dashboard** URL is printed in the console; it shows resources, logs, traces and metrics. The SQL Server password is generated by Aspire and stored in the AppHost user secrets.

### Sign in to the back office

Open the `BackOfficeApp` endpoint from the dashboard and sign in with a demo user:

| User | Password | Roles |
|---|---|---|
| `admin@example.com` | `Pass123$` | `PlatformAdmin`, `SystemAdmin` |

> [!WARNING]
> Demo users, their password (`Seed:DemoUsersPassword` in `appsettings.Development.json`) and the IdentityServer client secrets are **development-only** values.

### Switching the event bus to RabbitMQ

Azure Service Bus (emulator) is the default. To use RabbitMQ:

1. In `AppHost.cs`, replace `#define USE_AZURE_SERVICE_BUS` with `#define USE_RABBITMQ`.
2. In the infrastructure projects of Accounts, Notifications and Identity, reference `Platform.EventBus.RabbitMQ` instead of `Platform.EventBus.ServiceBus`.
3. In their service registration (`Infrastructure/Extensions/Extensions.cs` in Accounts and Notifications, `Extensions/Extensions.cs` in Identity), replace `builder.AddServiceBusEventBus("eventbus")` with `builder.AddRabbitMqEventBus("eventbus")`.

Handlers and integration events do not change. Note the [delivery differences](#️-project-status-and-known-limitations) between both transports.

### Running the tests

```bash
# Unit tests (no Docker required)
dotnet test tests/Platform/Platform.UnitTests
dotnet test tests/Services/Services.UnitTests

# Integration tests (Docker required, Testcontainers)
dotnet test tests/Platform/Platform.IntegrationTests

# Functional tests (Docker required, Aspire) — run as executables on the .NET 10 SDK
dotnet run --project tests/Platform/Platform.FunctionalTests
dotnet run --project tests/Services/Services.FunctionalTests
```

---

## 📖 Documentation

The documentation is organized in four sections; each has an index:

| Section | What you will find |
|---|---|
| [Architecture](docs/architecture/README.md) | How each building block works internally: layers, pipeline, domain events, idempotency, outbox, telemetry, event bus, read model, BFF |
| [Developer guide](docs/developer-guide/README.md) | How to build a service on top of them: configuration, transactional pipeline, an end-to-end walkthrough, testing, known gaps, AI-assisted development rules |
| [Sample feature](docs/features/README.md) | The example domain (customer onboarding & activation) and its cross-service workflow, API and UI |
| [Backlog](docs/backlog/README.md) | Designed but deliberately deferred improvements |

Suggested reading path:

| Step | Document |
|---|---|
| 1 | [Messaging & persistence layers](docs/architecture/01-messaging-persistence-layers.md) — the building blocks and their dependency rules |
| 2 | [Command pipeline](docs/architecture/02-command-pipeline.md) — Result pattern, CQRS and the behavior chain |
| 3 | [Transactional pipeline](docs/developer-guide/02-transactional-pipeline.md) — what runs inside and after the transaction |
| 4 | [End-to-end flow](docs/developer-guide/03-end-to-end-flow.md) — one request traced across two services |
| 5 | [Onboarding & activation workflow](docs/features/02-onboarding-activation-workflow.md) — a multi-step process built from these pieces |

---

## ⚠️ Project Status and Known Limitations

This is an evolving reference, not a finished product. Known limitations of the current code (full list in [Known gaps](docs/developer-guide/05-known-gaps.md)):

- **Failed results commit** — `TransactionBehavior` only rolls back on exceptions. A command that returns a failed `Result` (including validation errors) still commits, together with its idempotency entry, so a retry with the same `x-requestid` is treated as a duplicate.
- **Idempotency cache** — the Redis `processing` key is not removed when a command throws; a retry within its TTL (30 s by default) is answered as a duplicate.
- **Outbox recovery** — the `OutboxProcessor` retries events in `NotPublished` or `FailedToPublish` state at a fixed polling interval. Events left in `InProgress` (for example, if the process stops right after claiming them) are not retried.
- **RabbitMQ delivery** — the RabbitMQ consumer acknowledges messages even when a handler throws, and no dead-letter exchange is configured, so failed messages are lost. The Azure Service Bus implementation abandons the message, lets the broker retry it and dead-letters it after the maximum delivery count.
- **Event bus selection** — the transport is chosen in code (AppHost `#define` plus one registration per service), not through configuration.
- **Audit log** — entities get automatic `CreatedAt`/`UpdatedAt` and an optimistic concurrency `RowVersion`. A full audit log (`AuditLog` entity) is designed but not wired yet; see the [backlog](docs/backlog/audit-log-proposal.md).
- **Authorization** — there is no generic authorization behavior in the pipeline; endpoints use ASP.NET Core authorization policies.
- **Delivery guarantees** — the outbox and idempotent consumers protect the implemented paths; they do not provide end-to-end exactly-once delivery.
- **Sample shortcuts** — emails are only logged, the activation link carries no confirmation token, and APIs disable audience validation. See [Simplifications in the sample](docs/features/02-onboarding-activation-workflow.md#simplifications-in-the-sample).

---

## 🙏 Credits

- [dotnet/eShop](https://github.com/dotnet/eShop) — Microsoft's reference application whose patterns inspired the building blocks (MIT license).
- [Labs.Aspire.uSLearn](https://github.com/miquelalcaraz/Labs.Aspire.uSLearn) — the predecessor of this repository.
- [Aspire](https://learn.microsoft.com/dotnet/aspire/), [Duende IdentityServer](https://duendesoftware.com/products/identityserver), [Radzen Blazor](https://blazor.radzen.com/).

## 📄 License

Distributed under the [MIT](LICENSE) license. Some dependencies — notably **Duende IdentityServer**, **MediatR** and **FluentAssertions** — have their own commercial or dual licenses; see [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md) before reusing this code.

## 🤝 Contributing

This is a personal, educational reference, but suggestions and improvements are welcome through issues or pull requests.

## 👤 Author

**Miquel Alcaraz** — [GitHub](https://github.com/miquelalcaraz)
