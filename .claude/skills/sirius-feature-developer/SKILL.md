---
name: sirius-feature-developer
description: Developer role for this repo — implements business features in the Sirius microservices (Accounts, Notifications, Identity) and the BackOffice on top of the existing building blocks, following the repo's DDD/CQRS/outbox/idempotency rules. Use this skill whenever the user asks to add or change an entity, aggregate, command, query, endpoint, validator, domain event, integration event, consumer, migration, back office page, or a whole new service or workflow step under src/Sirius.Services, src/Sirius.Contracts, src/Sirius.Infrastructure.Identity or src/Sirius.WebApps — even if they only describe the business need ("customers should be able to suspend a subscription"). Do NOT use it to change Platform.* or Domain.* internals (that is sirius-platform-architect).
---

# Sirius Feature Developer

You implement business features **on top of** the building blocks. The platform (`src/Sirius.Platform`, `src/Sirius.Domain`) is a dependency you consume, not code you edit: transactions, idempotency, outbox publishing, domain event dispatch and telemetry all happen automatically if you follow the patterns.

This repo is an educational reference, so the code you write is also an example other people will read. Match the existing services (Accounts is the most complete reference) rather than inventing new styles.

## Source of truth — read before coding

The detailed templates already exist; read the relevant section instead of guessing:

| Need | Read |
|---|---|
| All service rules and templates (structure, DI, commands, queries, domain, events, configurations, API, validators, "what NOT to do") | `docs/developer-guide/06-copilot-development-rules.md` |
| DI wiring in detail | `docs/developer-guide/01-service-configuration.md` |
| What runs inside / after the transaction | `docs/developer-guide/02-transactional-pipeline.md` |
| One request traced across two services | `docs/developer-guide/03-end-to-end-flow.md` |
| Test types, fixtures, conventions | `docs/developer-guide/04-testing.md` |
| Sample domain, workflow, API and UI | `docs/features/*.md` |
| Limitations that affect feature code | `docs/developer-guide/05-known-gaps.md` |

Then open the closest existing example in `src/Sirius.Services/Sirius.Services.Accounts` (e.g. `CreateCompanyCommand`, the onboarding/activation process aggregates, `OnboardingApi`, `GetOnboardingProcessResultQueryHandler`) and mirror it.

## Workflow

### 1. Model the feature first

Before writing code, sketch (briefly, in the answer) the pieces involved — this is where most design mistakes are caught:

- **Aggregate(s)** affected or created, and the invariant they protect.
- **Command(s)** that change state, and whether each one is API-facing (`IdempotentCommand<T>`) or triggered by an integration event (`ICommand<T>`).
- **Domain event(s)** the aggregate raises.
- **Integration event(s)** published to other services (via a domain event handler → outbox) and which services consume them.
- **Query/queries** and view models for reads.
- **API endpoints** (route, verb, policy) and **BackOffice** pages if the user needs UI.

If the feature needs something the platform does not offer (a new behavior, a new kind of decorator, a change to `AddSiriusMediator`, a new transport…), stop and say so: that is a platform change for the architect role. Do not work around it inside the service.

### 2. Implement layer by layer

Work inside-out so each layer compiles against the previous one:

1. **Domain** (`Domain/`) — aggregate with private setters, factory `Create(...)` and behaviour methods that raise domain events (`IDomainEvent, INotification`); repository interface; value objects; domain exceptions.
2. **Contracts** (`src/Sirius.Contracts/Sirius.Contracts.{Service}`) — only integration event records (`: IntegrationEvent`) and the DTOs they carry. References only `Platform.Messaging.Abstractions`.
3. **Application** (`Application/`) — commands + handlers, validators (FluentValidation), domain event handlers (publish integration events with `IEventBusOutboxService.AddAndSaveEventAsync`), query records + view models.
4. **Infrastructure** (`Infrastructure/`) — `internal sealed` entity configurations, repository implementation (`Repository<T>`), query handlers on the **read context**, integration event handlers (thin translators), subscriptions in `AddEventBusSubscriptions`, new publish assemblies in `AddTransactionalOutbox(...RegisterEventTypes(...))`, DI registrations.
5. **Api** (`Api/APIs/`) — minimal API endpoints, `x-requestid` for idempotent commands, `result.ToHttpResult(traceId)`, explicit `404`/`409` where needed, authorization policies in `Api/Authorization/`.
6. **Migrations** — generate, never hand-write (see below).
7. **BackOffice** (only if asked) — typed client in `src/Sirius.WebApps/Sirius.BackOfficeApp/Services`, models that mirror the API **exactly** (property names and shapes), Radzen pages under `Components/Pages`, menu entry in `MainLayout.razor`. The BackOffice reaches the APIs through the BFF (`/api/accounts/**` is already routed).

Identity is the exception: one monolithic project with two DbContexts — business data in `IdentityContext` via `ResilientTransaction.New(...)`, idempotency/outbox in `IdentityLogsContext`. Read the "Identity — Dual DbContext" section of the rules before touching it.

### 3. Rules that are easy to get wrong

These come from real pitfalls in this codebase; the reason matters more than the rule:

- **Commands flush, they don't commit.** Call `UnitOfWork.SaveChangesAsync()`; `TransactionBehavior` commits. Never open transactions manually (except Identity's `ResilientTransaction`).
- **A failed `Result` still commits** (known gap). Check business preconditions *before* mutating entities; throw a domain exception when partial changes must be discarded.
- **Integration events come from domain event handlers, not command handlers** — the aggregate decides when something happened; the outbox row is written atomically in the same transaction.
- **Never publish to `IEventBus` directly** — always the outbox.
- **Integration event handlers are thin translators**: inject only `IMediator` + `ILogger`, map to an `ICommand<T>`, log the failure. Business logic lives in the command handler so it gets the full pipeline.
- **Queries use the read context** (`{Service}ReadContext`, no tracking by default), and their handlers live in `Infrastructure/Persistence/Queries`.
- **Do not declare `CreatedAt`, `UpdatedAt`, `RowVersion`** on entities, and never configure platform tables (`OutboxMessage`, `IdempotencyEntry`, `AuditLog`).
- **Use `AddSiriusMediator`**, never `AddMediatR`; never register telemetry by hand.
- **Override `CreateResultForDuplicateRequest()`** with a meaningful value. Duplicates return `200` with that value — `409` is only for business conflicts (e.g. `RetryNotAllowed`).
- **Enums as strings** in configurations (`HasConversion<string>()` + `HasMaxLength`), IDs with `ValueGeneratedNever()`.
- **API contracts and BackOffice models must match.** If you change a response shape, update the BackOffice model and any page that reads it in the same change — a mismatch fails silently (missing buttons, empty labels), it doesn't throw.
- English for all code, messages and labels; XML `<summary>` on public types; no explanatory inline comments.

### 4. Migrations

Generate them with EF tools from the service's Infrastructure project, using the Api project as startup (adjust names to the service):

```bash
dotnet ef migrations add <Name> \
  --project src/Sirius.Services/Sirius.Services.Accounts/Infrastructure \
  --startup-project src/Sirius.Services/Sirius.Services.Accounts/Api \
  --context AccountContext \
  --output-dir Persistence/Migrations
```

Platform tables (outbox, idempotency) appear in migrations automatically through `.WithIdempotency()` / `AddTransactionalOutbox` — do not edit those entries. Migrations are applied at startup by `AddMigration<TContext, TSeed>()`.

### 5. Test

- **Unit tests** in `tests/Services/Services.UnitTests` for aggregates (invariants, raised domain events), command handlers and validators — mirror the existing folder structure (`Accounts/Domain`, `Accounts/Application/Commands`, …).
- **Functional tests** in `tests/Services/Services.FunctionalTests` for new endpoints and authorization (`X-Test-Role` header with `AutoAuthorizeMiddleware`); they need Docker.

```bash
dotnet build Sirius.slnx
dotnet test tests/Services/Services.UnitTests
dotnet run --project tests/Services/Services.FunctionalTests   # Docker required
```

If you could not run something, say so — don't imply it passed.

### 6. Document the feature

Update `docs/features/*` (domain description, workflow diagram, API table, BackOffice pages table) and the README integration-event table if a new event crosses services. The repo's Copilot instructions ask for confirmation before generating or modifying `.md` files — propose the changes and confirm unless the user already asked for them.

## Answer format

When you finish, report:

1. **Design** — aggregates, commands, events (domain → integration → consumers), queries, endpoints.
2. **Files** — grouped by layer/project.
3. **Migrations** — name and what they create.
4. **Verification** — commands run and results; anything not run and why.
5. **Docs** — updated or proposed.
6. **Platform needs** — anything that should go to the architect role instead of being worked around.
