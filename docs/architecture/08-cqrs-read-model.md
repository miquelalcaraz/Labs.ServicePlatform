# CQRS Read Model

## Overview

The read side of CQRS uses a separate `QltReadDbContext` base class, fully isolated from write-side infrastructure. Queries use a dedicated context with no-tracking behavior and no access to `SaveChanges`, transactional outbox, or idempotency tables.

**Layer**: `Platform.Persistence.EntityFramework.Relational` (base class), `Platform.Persistence.EntityFramework` (configuration)  
**Key types**: `QltReadDbContext`, `QltReadDbContextConfiguration`, `AddQltReadDbContext<T>`

---

## Write vs Read Context

| | Write (`QltDBContext`) | Read (`QltReadDbContext`) |
|---|---|---|
| Base class | `QltDBContext` | `QltReadDbContext` |
| Tracking | Default (on) | `NoTrackingWithIdentityResolution` |
| `SaveChanges` | Transactional + audit | Throws `InvalidOperationException` |
| DI roles | `ITransactionalUnitOfWork`, `IDomainEntityChangeTracker` | Only its own type |
| EF model | Domain + infrastructure tables | Domain entities only |
| Connection | `accountdb` | `accountdb-readonly` (fallback to write) |

---

## Platform Components

### QltReadDbContextConfiguration

Defined in `Platform.Persistence.EntityFramework` (no provider dependency):

- `RegisterModelsFromAssembly` — registers domain entity configurations
- `SetDefaultSchema` — sets the default schema for entity mappings

Each read context gets its **own** `QltReadDbContextConfiguration` instance — it does not share the write context's configuration singleton.

### QltReadDbContext

Defined in `Platform.Persistence.EntityFramework.Relational` (requires EF Relational for `HasDefaultSchema`):

- Abstract base class with `NoTrackingWithIdentityResolution` as default query behavior
- `SaveChanges` / `SaveChangesAsync` overrides throw `InvalidOperationException`
- Sealed `SaveChanges` prevents accidental writes through the read context

---

## Usage

### Define a Read Context

```csharp
public class AccountReadContext : QltReadDbContext
{
    public DbSet<Organization> Organizations { get; set; } = null!;

    public AccountReadContext(
        DbContextOptions<AccountReadContext> options,
        QltReadDbContextConfiguration configuration)
        : base(options, configuration) { }
}
```

### Register in DI

```csharp
var readConnectionString = builder.Configuration.GetConnectionString("accountdb-readonly")
    ?? connectionString;

services.AddQltReadDbContext<AccountReadContext>(
    conf => conf
        .SetDefaultSchema("accounts")
        .RegisterModelsFromAssembly(typeof(AssemblyReference).Assembly),
    options => options.UseSqlServer(readConnectionString)
);
```

### Use in Queries

```csharp
public class OrganizationQueries(AccountReadContext context) : IOrganizationQueries
{
    public async Task<OrganizationDto?> GetByIdAsync(Guid id)
    {
        return await context.Organizations
            .Where(o => o.Id == id)
            .Select(o => new OrganizationDto { ... })
            .FirstOrDefaultAsync();
    }
}
```

`AsNoTracking()` is not needed — it is the default behavior from the base class.

---

## Production Scenario

For SQL Server AlwaysOn or Azure SQL read replicas:

```
accountdb-readonly: Server=...;ApplicationIntent=ReadOnly;...
```

If `accountdb-readonly` is not configured, the read context transparently falls back to the write connection string. No code changes required — purely infrastructure configuration.

---

## Validation Checklist

- [ ] Query classes inject the read context, not the write context
- [ ] Read context does not register infrastructure tables (outbox, idempotency, audit)
- [ ] `AddQltReadDbContext<T>` used for registration (not `AddQltSDBContext<T>`)
- [ ] No `SaveChanges` calls through read context (enforced by sealed override)
