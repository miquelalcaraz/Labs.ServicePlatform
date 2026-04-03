# 21 — Audit Log: Automatic ChangeTracker Proposal

## Status
**Proposed** — Not yet implemented. Placeholder POCO and EF configuration exist. The previous manual implementation has been removed.

## Context

The original audit implementation required developers to manually create `AuditLog` entries and attach them to entities via `Entity._audits`. This approach had several problems:

1. **Polluted `Entity` base class** — Every entity carried `List<AuditLog>`, `AuditLogs`, and `RemoveAudits()`, mixing infrastructure with domain
2. **Developer burden** — Each command handler had to manually construct audit entries with correct `userId`, `aggregateType`, etc.
3. **Inconsistent coverage** — Easy to forget, leading to gaps in audit trail
4. **Coupled to `IDomainEvent`** — `AuditLog.Changes` stored serialized domain events, conflating audit (what changed) with messaging (what happened)
5. **Serialization in the POCO** — `JsonSerializer` logic inside the domain model violated persistence ignorance

## Cleanup Performed

The following was removed to prevent the incomplete design from contaminating the codebase:

| Removed | Location |
|---------|----------|
| `_audits`, `AuditLogs`, `RemoveAudits()` | `Entity.cs` (Domain.Core) |
| `AuditLogsExtension.SaveAuditLogsAsync()` | `Platform.Persistence.EntityFramework` (deleted file) |
| `UseAuditLogs` property + `WithAuditLogs()` | `QltDbContextConfiguration` |
| `ConfigureAuditLogs()` / `SaveAuditLogsAsync()` calls | `QltDBContext.OnModelCreating` / `CommitTransactionAsync` |
| `AuditLog : Entity` inheritance, `IDomainEvent Changes`, `Create()` factory | `AuditLog.cs` → simplified to plain POCO |
| `Block` / `UnBlock` enum values | `AuditActionType` → moved to domain-specific enums if needed |

**Preserved:**
- `AuditLog` POCO (placeholder with new schema)
- `AuditLogConfiguration` (updated to match new POCO)
- `AuditActionType` enum (simplified: None, Create, Update, Delete)

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
public class Organization : Entity, IAggregateRoot, IAuditable
{
    // ...
}
```

Only entities implementing `IAuditable` are audited. This avoids auditing infrastructure entities (OutboxMessage, IdempotencyEntry, etc.).

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
    public string EntityType { get; set; }
    public string UserId { get; set; }
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
**Con:** Requires `IUserHttpContext` injection into interceptor (needs `IServiceProvider` in DbContext options).

#### Option B: Override in QltDBContext

Add audit capture directly in `CommitTransactionAsync` (where `SaveAuditLogsAsync` was previously called):

```csharp
// In CommitTransactionAsync, before commit:
var auditEntries = _auditService.CaptureChanges(ChangeTracker);
Set<AuditLog>().AddRange(auditEntries);
await base.SaveChangesAsync(cancellationToken);
```

**Pro:** Simple, no interceptor plumbing.  
**Con:** Couples audit to QltDBContext, requires a second `SaveChangesAsync` call.

### Edge Cases to Consider

1. **Background jobs / message consumers** — No `HttpContext`. `IUserHttpContext` returns null. Need a strategy: system user constant, or `IAuditContextProvider` abstraction that supports both HTTP and non-HTTP contexts
2. **Batch operations with `ExecuteUpdateAsync`** — Bypass ChangeTracker entirely. Not auditable through this mechanism. Document as known limitation or use DB triggers for batch audit
3. **Sensitive data** — Some properties should not appear in audit logs (passwords, tokens). Consider `[AuditIgnore]` attribute
4. **Performance** — Serializing changes on every save adds overhead. Consider: is audit needed on every DbContext, or only on specific ones?

### Estimated Effort

| Task | Effort |
|------|--------|
| `IAuditable` marker interface | Trivial |
| `AuditChangeTrackerService` (capture logic) | Medium |
| `SaveChangesInterceptor` or QltDBContext integration | Medium |
| `IUserHttpContext` null-safety for non-HTTP contexts | Low |
| Unit tests (interceptor + change capture) | Medium |
| Integration tests (full save + audit verification) | Medium |
| Apply `IAuditable` to existing entities | Low |
| Migration for updated audit table schema | Low |
| **Total** | **~1-2 days** |

## References

- [EF Core Interceptors — SaveChanges](https://learn.microsoft.com/en-us/ef/core/logging-events-diagnostics/interceptors#savechanges-interception)
- [EF Core Change Tracking](https://learn.microsoft.com/en-us/ef/core/change-tracking/)
- ABP Framework `IAuditedObject` pattern
- Clean Architecture audit trail patterns
