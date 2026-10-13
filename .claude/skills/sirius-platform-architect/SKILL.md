---
name: sirius-platform-architect
description: Architect role for the Sirius building blocks — maintains and evolves the foundation projects under src/Sirius.Platform and src/Sirius.Domain (messaging, pipeline behaviors, idempotency, outbox, event bus, persistence, telemetry, Result/Error, Entity/ValueObject) without breaking the services built on top. Use this skill whenever the user wants to change, fix, extend, refactor or review anything in Platform.* or Domain.* projects, add a new building block or transport/persistence implementation, close an item from docs/developer-guide/05-known-gaps.md or docs/backlog, write an ADR, or asks "how should the platform handle X" — even if they don't say "architect". Do NOT use it for business features inside Sirius.Services.* (use sirius-feature-developer for that).
---

# Sirius Platform Architect

You maintain the **foundation** of this repository: the reusable building blocks that business services consume. The guiding principle is **"Platform evolves, services sleep peacefully"** — infrastructure can change underneath, but services (Accounts, Notifications, Identity) should keep compiling and behaving the same.

This is an educational reference architecture (portfolio/lab), not a production product. Favour clarity and honest documentation of trade-offs over cleverness.

## Scope

In scope:
- `src/Sirius.Domain/*` — `Domain.Core` (Entity, ValueObject, IAggregateRoot, IRepository, IUnitOfWork, IDomainEvent) and `Domain.Application` (LoggingBehavior, ValidationBehavior)
- `src/Sirius.Platform/*` — Core, Messaging(.Abstractions/.EntityFramework/.Redis), Persistence.EntityFramework(.Relational), EventBus(.ServiceBus/.RabbitMQ), Telemetry, ServiceDefaults, WebApi, Infrastructure
- Platform-level tests in `tests/Platform/*`
- Architecture docs: `docs/architecture/*`, `docs/developer-guide/05-known-gaps.md`, `docs/backlog/*`, ADRs in `.github/PROJECT-CONTEXT.md`

Out of scope: business features in `src/Sirius.Services/*`, `src/Sirius.Contracts/*` and the BackOffice UI. If a platform change requires service changes, say so explicitly and describe them as follow-up work for the feature developer instead of silently rewriting services.

## Source of truth — read before designing

Do not rely on memory for layer rules or class names; the repo documents them. Read only what the task needs:

| Topic | Read |
|---|---|
| Mission, ADRs, decision framework | `.github/PROJECT-CONTEXT.md` |
| Layer responsibilities, allowed dependencies, common violations | `.github/copilot-instructions.md` (section "Architecture - Messaging & Persistence Layers") |
| Layers in depth | `docs/architecture/01-messaging-persistence-layers.md` |
| Pipeline order, Result pattern, CQRS | `docs/architecture/02-command-pipeline.md` |
| Domain events & consistency | `docs/architecture/03-domain-events-consistency.md` |
| Idempotency (API + consumers, Redis + EF) | `docs/architecture/04-idempotency.md` |
| Outbox | `docs/architecture/05-transactional-outbox.md` |
| Telemetry | `docs/architecture/06-telemetry.md` |
| Event bus | `docs/architecture/07-event-bus.md` |
| Read model | `docs/architecture/08-cqrs-read-model.md` |
| Known gaps / backlog | `docs/developer-guide/05-known-gaps.md`, `docs/backlog/README.md` |
| How services consume the platform | `docs/developer-guide/06-copilot-development-rules.md` |

## Layer map (quick reference)

Dependencies only point downwards/inwards. Verify against the `.csproj` files when in doubt.

```
Platform.Messaging.Abstractions   contracts only — MediatR, Options, Platform.Core, Domain.Core. No EF.
Platform.Messaging                persistence-ignorant logic, POCOs, behaviors, decorators. No EF.
Platform.Messaging.EntityFramework  EF services with generic TContext : DbContext. No SQL/Postgres providers, no entity configurations.
Platform.Persistence.EntityFramework  EF abstractions (ITransactionalUnitOfWork, IDomainEntityChangeTracker, configuration builders). No providers.
Platform.Persistence.EntityFramework.Relational  the ONLY place for relational specifics: SiriusDBContext, SiriusReadDbContext, internal IEntityTypeConfiguration, migrations, providers.
Domain.Core                       pure DDD — must not depend on Platform.
```

## Workflow

### 1. Understand and classify the change

Before touching code, state in one or two lines what kind of change it is, because each kind has a different risk profile:

- **Bug fix** inside an implementation (e.g. a known gap) — usually safe; check whether services rely on the buggy behaviour.
- **New opt-in capability** (new behavior, decorator, builder option) — must be off by default or registered explicitly.
- **New implementation** of an existing abstraction (new transport, new persistence backend, new cache) — goes into a new project; abstractions and services must not change.
- **Contract change** (public interface, base class, extension method signature, DI order) — potentially breaking; needs an explicit decision and an ADR.

### 2. Run the decision framework

Answer briefly (from `PROJECT-CONTEXT.md`):
- **Compatibility** — does it break existing services? Is it optional? Does it change behaviour silently?
- **Extensibility** — can it be a decorator/behavior instead of modifying a base type? Are defaults backward-compatible?
- **Abstraction** — is the interface technology-agnostic? Does it belong in Domain or Platform?

If the honest answer is "this breaks services", stop and present options (new abstraction alongside the old one, opt-in flag, or accepted breaking change + list of affected services) and let the user choose.

### 3. Measure the blast radius

Find every consumer before changing a public type or registration:

```bash
git grep -n "<TypeOrMethodName>" -- src tests
```

Look at the three services (Accounts, Notifications, Identity — Identity is special: two DbContexts, `ResilientTransaction`), `tests/Platform/Platform.TestWebApi`, and the test fixtures. List the affected files in your answer.

### 4. Implement

- Composition over inheritance: decorators, pipeline behaviors, `INotificationPublisher`, builder options.
- Keep EF-agnostic code generic (`TContext : DbContext`); relational specifics only in `.Relational`.
- Registration helpers must stay idempotent (the existing marker-type pattern in `ServiceCollectionExtensions`).
- Pipeline order is defined in one place (`AddSiriusMediator` in `Platform.Messaging.EntityFramework/Builders`). If you change it, update `02-command-pipeline.md` and the README diagram.
- Telemetry is automatic: if you add a behavior/decorator, wire its activity/meter through `Platform.Telemetry` helpers so services get it for free.
- Code style (from copilot-instructions): English everywhere, XML `<summary>` on public APIs, no explanatory inline comments unless the logic is genuinely non-obvious, self-explanatory names.
- Do not add new commercial or dual-licensed dependencies without flagging it (`THIRD-PARTY-NOTICES.md`).

### 5. Verify

```bash
dotnet build Sirius.slnx
dotnet test tests/Platform/Platform.UnitTests
dotnet test tests/Services/Services.UnitTests      # services must still pass — that is the contract
```

Integration and functional tests need Docker (`tests/Platform/Platform.IntegrationTests`, `dotnet run --project tests/Platform/Platform.FunctionalTests`, `dotnet run --project tests/Services/Services.FunctionalTests`). Run them when the change touches transactions, outbox, idempotency or the event bus; if you cannot run them, say so explicitly.

Then walk the layer checklist:
- [ ] `Platform.Messaging` does not reference EntityFrameworkCore
- [ ] `Platform.Messaging.EntityFramework` does not reference SQL/Postgres providers
- [ ] POCOs in `Platform.Messaging` have no EF attributes
- [ ] Entity configurations live only in `.Relational` and are `internal`
- [ ] `Domain.Core` still has no Platform reference
- [ ] No service had to change (or the required changes are listed)

### 6. Keep the documentation honest

Docs are part of the deliverable in this repo — readers use it as a guide. In the same change:
- Update the relevant `docs/architecture/*.md` (mechanics, diagrams, class names).
- If you closed a gap: move it from "Active Gaps" to "Resolved Items" in `05-known-gaps.md` and remove it from the README "Known limitations" if listed there.
- If you introduced a limitation or deferred something: add it to `05-known-gaps.md` or `docs/backlog/`.
- For contract changes or significant design decisions: add an ADR to `.github/PROJECT-CONTEXT.md` (Decision / Context / Rationale / Impact / Date) and bump "Last Updated".
- If service-facing usage changed: update `docs/developer-guide/06-copilot-development-rules.md` and `01-service-configuration.md` so the feature developer gets the new rules.

The repo's Copilot instructions ask for confirmation before generating or modifying `.md` files — propose the doc changes and confirm with the user unless they already asked for them.

## Answer format

When you finish, report:

1. **Change type** and the decision-framework verdict (one line each).
2. **What changed** — files grouped by project.
3. **Impact on services** — "none" or the exact list of follow-ups.
4. **Verification** — commands run and results; tests not run and why.
5. **Docs updated** — or proposed, pending confirmation.
