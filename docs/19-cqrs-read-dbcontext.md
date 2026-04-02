# CQRS Read-Only DbContext

## Summary

Separate `QltReadDbContext` base class for the read side of CQRS. Queries use a dedicated context connected to a read-only connection string, fully isolated from write-side infrastructure.

**Status**: ✅ Implemented  
**Impact**: Clean CQRS read/write separation at the persistence layer

---

## Problem

`OrganizationQueries` was injecting `AccountContext` (the write context), which carries:
- Transaction management (`ITransactionalUnitOfWork`)
- Infrastructure tables: idempotency, outbox, audit logs
- `SaveChangesAsync` override with unique constraint detection

All unused overhead for a simple `SELECT`.

---

## Solution

| | Write (`AccountContext`) | Read (`AccountReadContext`) |
|---|---|---|
| Base class | `QltDBContext` | `QltReadDbContext` |
| Tracking | Default (on) | `NoTrackingWithIdentityResolution` |
| `SaveChanges` | Transactional + audit | `throws InvalidOperationException` |
| DI roles | `ITransactionalUnitOfWork` | Only its own type |
| EF model | Domain + infra tables | Domain entities only |
| Connection | `accountdb` | `accountdb-readonly` (fallback to write) |

---

## New Platform Components

### `Platform.Persistence.EntityFramework`
```
Options/QltReadDbContextConfiguration.cs   ← RegisterModelsFromAssembly + SetDefaultSchema only
```

### `Platform.Persistence.EntityFramework.Relational`
```
QltReadDbContext.cs                        ← Abstract base, NoTracking, SaveChanges throws
Builders/Extensions.cs                    ← AddQltReadDbContext<T>()
```

---

## Usage

```csharp
// Infrastructure/Extensions.cs
var readConnectionString = builder.Configuration.GetConnectionString("accountdb-readonly")
    ?? connectionString;

services.AddQltReadDbContext<AccountReadContext>(
    conf => conf
        .SetDefaultSchema("accounts")
        .RegisterModelsFromAssembly(typeof(AssemblyReference).Assembly),
    options => options.UseSqlServer(readConnectionString)
);
```

```csharp
// AccountReadContext.cs
public class AccountReadContext : QltReadDbContext
{
    public DbSet<Organization> Organizations { get; set; } = null!;

    public AccountReadContext(
        DbContextOptions<AccountReadContext> options,
        QltReadDbContextConfiguration configuration)
        : base(options, configuration) { }
}
```

```csharp
// OrganizationQueries.cs — AsNoTracking() removed (implicit)
public class OrganizationQueries(AccountReadContext context) : IOrganizationQueries { ... }
```

---

## Key Constraints

- `AddQltReadDbContext<T>` creates its **own** `QltReadDbContextConfiguration` instance — does not share the write singleton.
- `QltReadDbContext` is in `Platform.Persistence.EntityFramework.Relational` (requires `HasDefaultSchema` from EF Relational).
- `QltReadDbContextConfiguration` is in `Platform.Persistence.EntityFramework` (no provider dependency).
- If `accountdb-readonly` is not configured, falls back to the write connection string transparently.

---

## Production Scenario

For SQL Server AlwaysOn or Azure SQL read replicas, set `ApplicationIntent=ReadOnly` in the read connection string:

```
accountdb-readonly: Server=...;ApplicationIntent=ReadOnly;...
```

No code changes required — purely infrastructure configuration.
