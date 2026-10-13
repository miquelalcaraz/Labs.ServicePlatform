# Audit Log — Automatic ChangeTracker Proposal

> **Status**: Proposed — not implemented. `AuditLog` (placeholder POCO) and `AuditLogConfiguration` exist but are not applied to any context.
> **Related**: [01 — Messaging & Persistence Layers](../architecture/01-messaging-persistence-layers.md), [Known Gaps](../developer-guide/05-known-gaps.md)

Every entity already gets `CreatedAt` / `UpdatedAt` and an optimistic concurrency `RowVersion` automatically. This proposal covers the next step: a persistent record of **who changed what**.

## Context

An earlier version of the platform had a manual audit implementation: developers created `AuditLog` entries and attached them to entities through `Entity._audits`. It was removed because:

1. **It polluted the `Entity` base class** — every entity carried an audit collection, mixing infrastructure with domain.
2. **It put the burden on developers** — each command handler had to build audit entries by hand, with the correct user and aggregate type.
3. **Coverage was inconsistent** — easy to forget, leading to gaps in the audit trail.
4. **It was coupled to `IDomainEvent`** — `AuditLog.Changes` stored serialized domain events, conflating audit (*what changed*) with messaging (*what happened*).
5. **It broke persistence ignorance** — serialization logic lived inside the domain model.

Only the simplified `AuditLog` POCO, its `AuditLogConfiguration` and the `AuditActionType` enum (`None`, `Create`, `Update`, `Delete`) were kept as a starting point.

## Proposed Design: Automatic Audit via EF ChangeTracker

### Core Idea

Intercept `SaveChangesAsync` to automatically capture entity changes using EF's `ChangeTracker`, enriched with user identity from `IUserHttpContext`.

### Architecture

```
CommitTransactionAsync / SaveChangesAsync
  └─ AuditInterceptor (SaveChangesInterceptor or override)
       ├─ ChangeTracker.Entries<IAuditable>()
       │    ├─ EntityState.Added    → AuditActionType.Create
       │    ├─ EntityState.Modified → AuditActionType.Update
       │    └─ EntityState.Deleted  → AuditActionType.Delete
       ├─ IUserHttpContext.GetUserIdentity() → UserId
       ├─ Capture OldValues / NewValues → JSON
       └─ Add AuditLog entries to same transaction
```

### Key Design Decisions

#### 1. Opt-in via marker interface

```csharp
// Domain.Core
public interface IAuditable { }

// Domain entity
public class Company : Entity, IAggregateRoot, IAuditable
{
    // ...
}
```

Only entities implementing `IAuditable` are audited. This avoids auditing infrastructure entities (`OutboxMessage`, `IdempotencyEntry`, etc.).

#### 2. Changes format: OldValues / NewValues JSON

```json
{
  "Name": { "Old": "Acme Corp", "New": "Acme Inc" },
  "TaxId": { "Old": null, "New": "B12345678" }
}
```

For `Create`: only `New` values. For `Delete`: only `Old` values. For `Update`: only modified properties.

This is independent of domain events — audit captures **data changes**, not **domain intent**.

#### 3. AuditLog POCO (current placeholder)

```csharp
public class AuditLog
{
    public Guid Id { get; set; }
    public Guid EntityId { get; set; }
    public string EntityType { get; set; } = null!;
    public string UserId { get; set; } = null!;
    public AuditActionType ActionType { get; set; }
    public DateTime Timestamp { get; set; }
    public string? Changes { get; set; }  // JSON with Old/New values
}
```

#### 4. Layer placement

| Component | Layer | Rationale |
|-----------|-------|-----------|
| `IAuditable` | Domain.Core | Marker interface, no dependencies |
| `AuditLog` POCO | Domain.Core/SeedWork | Pure POCO, already there |
| `AuditActionType` | Domain.Core/SeedWork | Simple enum, already there |
| `IAuditInterceptor` / service interface | Platform.Persistence.EntityFramework | EF abstraction, no DB specifics |
| Interceptor implementation | Platform.Persistence.EntityFramework | Uses `ChangeTracker` API (provider-agnostic) |
| `AuditLogConfiguration` | Platform.Persistence.EntityFramework.Relational | Table/index definitions (already there) |

#### 5. IUserHttpContext injection

`IUserHttpContext` already exists in `Domain.Application` and is implemented by `UserHttpContext` in `Platform.Infrastructure`. The interceptor/service receives it via DI:

```csharp
public class AuditChangeTrackerService(IUserHttpContext userContext)
{
    public IReadOnlyList<AuditLog> CaptureChanges(ChangeTracker changeTracker)
    {
        var userId = userContext.GetUserIdentity();
        var entries = changeTracker.Entries<IAuditable>()
            .Where(e => e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted);

        // Build AuditLog entries from OriginalValues / CurrentValues...
    }
}
```

### Implementation Options

#### Option A: SaveChangesInterceptor (recommended)

EF Core `SaveChangesInterceptor` — clean separation, registered via DI, no DbContext modification needed:

```csharp
public class AuditSaveChangesInterceptor : SaveChangesInterceptor
{
    // Capture changes in SavingChangesAsync (before save)
    // Add AuditLog entries to context
    // They get saved in the same transaction
}
```

**Pro:** Clean separation, testable, follows EF Core conventions.
**Con:** Requires `IUserHttpContext` injection into the interceptor (needs `IServiceProvider` in DbContext options).

#### Option B: Override in MiqaDBContext

Add audit capture directly in `CommitTransactionAsync`:

```csharp
// In CommitTransactionAsync, before commit:
var auditEntries = _auditService.CaptureChanges(ChangeTracker);
Set<AuditLog>().AddRange(auditEntries);
await base.SaveChangesAsync(cancellationToken);
```

**Pro:** Simple, no interceptor plumbing.
**Con:** Couples audit to `MiqaDBContext`, requires a second `SaveChangesAsync` call.

### Edge Cases to Consider

1. **Background jobs / message consumers** — No `HttpContext`, so `IUserHttpContext` returns null. Needs a strategy: a system user constant, or an `IAuditContextProvider` abstraction that supports both HTTP and non-HTTP contexts.
2. **Batch operations with `ExecuteUpdateAsync`** — Bypass the ChangeTracker entirely. Not auditable through this mechanism; document as a known limitation or use DB triggers for batch audit.
3. **Sensitive data** — Some properties should not appear in audit logs (passwords, tokens). Consider an `[AuditIgnore]` attribute.
4. **Performance** — Serializing changes on every save adds overhead. Consider whether audit is needed on every DbContext or only on specific ones.

## References

- [EF Core Interceptors — SaveChanges](https://learn.microsoft.com/en-us/ef/core/logging-events-diagnostics/interceptors#savechanges-interception)
- [EF Core Change Tracking](https://learn.microsoft.com/en-us/ef/core/change-tracking/)
- ABP Framework `IAuditedObject` pattern
