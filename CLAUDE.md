# Sirius — AI Assistant Instructions

Shared instructions for AI assistants working in this repository (Claude Code reads this file directly; GitHub Copilot is pointed here from `.github/copilot-instructions.md`).

## ⚠️ Very important

**Before generating or modifying any `.md` file, ask the user for confirmation** — unless they already explicitly asked for that documentation change.

## 📖 Where the rules live

| Need | Read |
|---|---|
| Mission, "Platform evolves, services sleep peacefully", decision framework, ADRs | [docs/architecture/00-principles-and-adrs.md](docs/architecture/00-principles-and-adrs.md) |
| Messaging & persistence layer responsibilities, allowed dependencies | [docs/architecture/01-messaging-persistence-layers.md](docs/architecture/01-messaging-persistence-layers.md) |
| Rules and templates for service code (`Sirius.Services.*`) | [docs/developer-guide/06-copilot-development-rules.md](docs/developer-guide/06-copilot-development-rules.md) |
| Idempotency API conventions (`200` for duplicates, never `409`) | [docs/architecture/04-idempotency.md](docs/architecture/04-idempotency.md) |
| Known limitations | [docs/developer-guide/05-known-gaps.md](docs/developer-guide/05-known-gaps.md) |

Claude Code skills (`.claude/skills/`):
- `sirius-platform-architect` — changes to `src/Sirius.Platform` and `src/Sirius.Domain`.
- `sirius-feature-developer` — business features in services, contracts, Identity and the BackOffice.

**Before making architectural decisions or modifying base components, read the principles & ADRs document.** Significant decisions are recorded there as new ADRs.

## Project overview

- Microservices architecture with reusable building blocks in `src/Sirius.Platform` and `src/Sirius.Domain`, consumed as project references (not yet packaged as NuGets) by the Accounts, Notifications and Identity services.
- .NET 10, C# 14, Aspire, Blazor + Radzen.
- Patterns: Result (railway-oriented), CQRS with MediatR (`ICommand<T>`, `IQuery<T>`), domain events (`IDomainEvent`), unified idempotency (`IIdempotencyService`, DB + cache, DB-enforced unique constraint), transactional outbox, idempotent consumer, unit of work, pipeline behaviors, eventual consistency.
- **Key principle:** infrastructure changes must be transparent to business services through abstraction layers and dependency inversion.

## General guidelines

- All code — including comments, messages, labels and strings — is written in **English**, even when the conversation with the user is in Spanish.
- Build stable foundational components that minimize breaking changes for the services built on top.

## Code style and comments

- **No inline comments in code blocks** — code must be self-explanatory through clear naming and structure.
- **XML documentation (`<summary>`)** on public APIs, methods and classes — concise and precise.
- **Extensive explanations belong in `.md` documentation**, not in code comments.
- **Exception:** inline comments only for genuinely complex algorithms or non-obvious business rules that refactoring cannot clarify.
- **Prefer** refactoring for clarity over adding explanatory comments.

Illustrative example (simplified, not the actual `IdempotencyEfService` API):

```csharp
// ❌ BAD - Unnecessary comments
public Task CreateRequestForCommandAsync<T>(Guid id)
{
    // ✅ IMPORTANT: Do NOT call SaveChangesAsync() here
    // The idempotency entry is added to ChangeTracker...
    // (10 lines of comments explaining the pattern)

    var entry = new IdempotencyEntry { ... };
    _context.Add(entry);
    return Task.CompletedTask;
}

// ✅ GOOD - Self-explanatory code with concise summary
/// <summary>
/// Marks a command as being processed by adding an idempotency entry to the change tracker.
/// </summary>
public Task CreateRequestForCommandAsync<T>(Guid id)
{
    var entry = new IdempotencyEntry
    {
        Id = id,
        Scope = IdempotencyScope.Request,
        Qualifier = typeof(T).Name
    };

    _context.Add(entry);
    return Task.CompletedTask;
}
```

## Layer violations to avoid

Full layer rules: [01-messaging-persistence-layers.md](docs/architecture/01-messaging-persistence-layers.md).

1. **EF attributes on POCOs in `Platform.Messaging`** — POCOs stay persistence-ignorant (no `[Key]`, `[Column]`, `[MaxLength]`…).
2. **SQL-specific code in `Platform.Messaging.EntityFramework`** — use the EF Core API (`context.Set<T>().FirstOrDefaultAsync(...)`), never `ExecuteSqlRawAsync`.
3. **Table names or mappings outside entity configurations** — `ToTable`, keys and indexes live only in `internal` configurations in `Platform.Persistence.EntityFramework.Relational/Configurations/`.
4. **Concrete `DbContext` in platform services** — use `TContext where TContext : DbContext`.

Checklist before committing changes to messaging/persistence layers:

- [ ] `Platform.Messaging` does NOT reference `EntityFrameworkCore`
- [ ] `Platform.Messaging.EntityFramework` does NOT reference SQL/Postgres providers
- [ ] POCOs in `Platform.Messaging` have NO EF attributes
- [ ] Entity configurations are in `Platform.Persistence.EntityFramework.Relational` ONLY
- [ ] Services in `Platform.Messaging.EntityFramework` use generic `TContext where TContext : DbContext`
- [ ] No SQL-specific code outside `Platform.Persistence.EntityFramework.Relational`

## Razor pages and components

When modifying Razor pages or components, consult the Radzen Blazor documentation first (in GitHub Copilot, via `#radzen_search`) to follow the component library's best practices.

## Idempotency — API response rules

- Always override `CreateResultForDuplicateRequest()` with a meaningful value (e.g. `Result.Success(true)` for creation commands); the default returns `default(TResponse)`.
- Duplicate requests (same `x-requestid`) return **`200 OK`** with that value — never `409 Conflict`. Same status for the 1st and Nth call, no special client logic (industry standard: Stripe, PayPal, AWS, GitHub, Azure).
- `409 Conflict` is only for **business** conflicts on a new request (e.g. `RetryNotAllowed`, `ResendNotAllowed`).
- Returning full DTOs on duplicates (querying the entity by `RequestId`) is only worth it when clients need the object, the `RequestId` is stored on the entity and the extra query is acceptable.
- An `X-Idempotency-Replayed` header is **not implemented** — wait for a real use case (YAGNI).
