# Copilot Instructions

# **Very important:**
<aside>
💡
Before generating or modifying documents with the .md extension, it is essential to request confirmation.
</aside>

## 📖 Project Context
**CRITICAL:** Before making architectural decisions or modifying base components, read [PROJECT-CONTEXT.md](PROJECT-CONTEXT.md).

This document explains:
- The primary mission: Build stable components that evolve without breaking services
- Architectural principles and decision framework
- Non-breaking evolution strategies

## 🛠️ Service Development
**When implementing features in `Miqa.Services.*` microservices**, follow the rules in [Copilot Development Rules](../docs/developer-guide/06-copilot-development-rules.md).

This document defines:
- Service project structure (Api, Application, Domain, Infrastructure, Contracts)
- Command, Query, Domain Event, and Integration Event patterns
- DI configuration templates
- Entity configuration rules and what NOT to create (platform tables)
- API endpoint patterns
- Success metrics for the base architecture

**Key principle:** "Platform evolves, services sleep peacefully" - Infrastructure changes must be transparent to business services through proper abstraction layers and dependency inversion.

## General Guidelines
- All code including comments, messages, labels, and strings must be written in English, regardless of the conversation language with the user being in Spanish.
- The primary goal is to create stable foundational components that minimize breaking changes when services are built on top. Components must be designed to evolve without drastically impacting existing services. Infrastructure changes must be transparent to business services through proper abstraction layers and dependency inversion.

## Code Style and Comments
- **NO inline comments in code blocks** - Code must be self-explanatory through clear naming and structure
- **Use XML documentation (`<summary>`)** for public APIs, methods, and classes - Keep them concise and precise
- **Extensive explanations belong in .md documentation files**, not in code comments
- **Exception**: Only add inline comments for truly complex algorithms or non-obvious business rules that cannot be clarified through refactoring
- **Prefer**: Refactoring code to be clearer over adding explanatory comments

### Examples

```csharp
// ❌ BAD - Unnecessary comments
public Task CreateRequestForCommandAsync<T>(Guid id)
{
    // ✅ IMPORTANT: Do NOT call SaveChangesAsync() here
    // The idempotency entry is added to ChangeTracker...
    // (10 lines of comments explaining the pattern)

    var request = new RequestIdempotency { ... };
    _context.Add(request);
    return Task.CompletedTask;
}

// ✅ GOOD - Self-explanatory code with concise summary
/// <summary>
/// Marks a command as being processed by adding an idempotency entry to the change tracker.
/// </summary>
public Task CreateRequestForCommandAsync<T>(Guid id)
{
    var request = new RequestIdempotency
    {
        RequestId = id,
        CommandName = typeof(T).Name,
        ProcessedAt = DateTime.UtcNow
    };

    _context.Add(request);
    return Task.CompletedTask;
}
```

## Project Guidelines
- Working on a microservices architecture with reusable NuGet packages. Currently implementing core components in the "Platform" and "Domain" folders as projects (not yet packaged as NuGets) referenced by initial services like Accounts and Identity. 
- Project uses .NET 10, C# 14.0, and Blazor. Implementing patterns like idempotency, domain events, and integration events.
- **Critical Patterns Implemented**: 
  - Result Pattern for Railway-Oriented Programming
  - CQRS with MediatR (Commands with ICommand<T>)
  - Domain Events with IDomainEvent
  - Idempotency with database-enforced unique constraint
  - Unified Idempotency with IIdempotencyService (DB + Cache)
  - Transactional Outbox
  - Idempotent Consumer
  - Unit of Work
  - Pipeline Behavior
  - Eventual Consistency

## Architecture - Messaging & Persistence Layers

### ⚠️ CRITICAL: Layer Responsibilities and Separation of Concerns

The project follows a **5-layer architecture** for messaging and persistence. **NEVER violate these boundaries**:

#### 1️⃣ Platform.Messaging.Abstractions (Pure Contracts)
**Responsibilities:**
- Define interfaces: `IIntegrationEventHandler<T>`, `ICommandHandler<T,R>`, `IDomainEventHandler<T>`
- Define service contracts: `IIdempotencyService`, `IEventBusOutboxService`
- Define base types: `IntegrationEvent`, `ICommand`, `IDomainEvent`

**MUST NOT:**
- ❌ Reference Entity Framework (`Microsoft.EntityFrameworkCore`)
- ❌ Reference database providers (SQL Server, PostgreSQL, Cosmos)
- ❌ Contain implementation logic (services, repositories)
- ❌ Know about persistence details

**Dependencies allowed:** MediatR, Microsoft.Extensions.Options, Platform (base types)

---

#### 2️⃣ Platform.Messaging (Messaging Logic - No Persistence)
**Responsibilities:**
- Domain models as POCOs: `IdempotencyEntry`, `OutboxMessage`, `OutboxMessageStatus`
- MediatR behaviors: `IdempotencyBehavior<TRequest, TResponse>`, `TelemetryCommandBehavior`, `TelemetryQueryBehavior`
- Domain event telemetry: `TelemetryDomainEventBehavior` (custom `INotificationPublisher`)
- Domain event dispatch: `DomainEventsDispatcherExtension` (IMediator extension method)
- Decorators without persistence: `TelemetryIntegrationEventHandlerDecorator<T>`, `IdempotencyIntegrationEventDecorator<T>`
- Command abstractions: `IdempotentCommand<T>`
- DI registration extensions

**MUST NOT:**
- ❌ Reference Entity Framework (`Microsoft.EntityFrameworkCore`)
- ❌ Reference database providers
- ❌ Have EF attributes on models (`[Table]`, `[Key]`, etc.)
- ❌ Implement persistence logic
- ❌ Use `DbContext` or `DbSet<T>`

**Dependencies allowed:** Platform.Messaging.Abstractions, Platform.Telemetry, MediatR, Scrutor

**Key principle:** POCOs are **persistence-ignorant**. Entity configurations belong in `Platform.Persistence.EntityFramework.Relational`.

---

#### 3️⃣ Platform.Messaging.EntityFramework (Messaging + EF, DB-Agnostic)
**Responsibilities:**
- Services using EF with generic constraints: `IdempotencyEfService<TContext>`, `OutboxMessageService<TContext>`, `EventBusOutboxService<TContext>`
- Decorators with persistence: `IdempotencyIntegrationEventEfDecorator<T>`
- Pipeline behaviors: `TransactionBehavior<TRequest, TResponse>`, `DomainEventProcessorBehavior<TRequest, TResponse>`, `IntegrationEventProcessorBehavior<TRequest, TResponse>`
- Background services: `OutboxProcessor` (hosted service for pending outbox events)
- DI registration for EF-based services

**MUST:**
- ✅ Use generic `TContext where TContext : DbContext`
- ✅ Work with any DbContext implementation
- ✅ Use EF Core API (`DbSet<T>`, `SaveChangesAsync()`, transactions)

**MUST NOT:**
- ❌ Reference SQL Server (`Microsoft.Data.SqlClient`)
- ❌ Reference PostgreSQL (`Npgsql`)
- ❌ Reference Cosmos (`Microsoft.EntityFrameworkCore.Cosmos`)
- ❌ Contain Entity Configurations (`IEntityTypeConfiguration<T>`)
- ❌ Know about table names, schemas, indexes, constraints
- ❌ Use SQL-specific features (stored procedures, raw SQL)

**Dependencies allowed:** Platform.Messaging, Platform.Persistence.EntityFramework, Microsoft.EntityFrameworkCore, Platform.EventBus, Platform.Telemetry

**Key principle:** Works with **any EF-compatible database**. Database-specific logic belongs in `Platform.Persistence.EntityFramework.Relational` or future layers (Cosmos, etc.).

---

#### 4️⃣ Platform.Persistence.EntityFramework (EF Abstractions)
**Responsibilities:**
- Transaction interfaces: `ITransactionalUnitOfWork` (extends `IUnitOfWork` from Domain.Core with transaction capabilities)
- Domain entity tracking: `IDomainEntityChangeTracker` (exposes tracked entities for domain event dispatch)
- Configuration: `MiqaDbContextConfiguration`, `MiqaReadDbContextConfiguration` (fluent API for features)
- Exception abstractions: `UniqueConstraintViolationException`

**MUST NOT:**
- ❌ Reference database providers (SQL Server, PostgreSQL, Cosmos)
- ❌ Contain Entity Configurations
- ❌ Implement database-specific logic
- ❌ Know about table schemas, indexes

**Dependencies allowed:** Microsoft.EntityFrameworkCore, MediatR, Domain.Core, Platform.Telemetry

**Key principle:** Pure EF abstractions without database specifics.

---

#### 5️⃣ Platform.Persistence.EntityFramework.Relational (SQL/PostgreSQL Specifics)
**Responsibilities:**
- Concrete DbContext: `MiqaDBContext` with relational features
- Entity Configurations: `IdempotencyEntryConfiguration`, `OutboxMessageConfiguration`, `AuditLogConfiguration`
- Resilient transactions: `ResilientTransaction` (retry logic)
- Generic repositories: `Repository<T>`
- Migrations: `MigrateDbContextExtensions`
- DI registration: `AddMiqaSDBContext<TContext>()`

**MUST:**
- ✅ Configure table names, schemas, indexes, constraints
- ✅ Use `IEntityTypeConfiguration<T>` for mappings
- ✅ Handle SQL Server and PostgreSQL specifics
- ✅ Keep configurations `internal` (not public)

**CAN:**
- ✅ Reference `Microsoft.Data.SqlClient` and `Npgsql`
- ✅ Use relational-specific features

**Dependencies allowed:** Platform.Persistence.EntityFramework, Platform.Messaging.EntityFramework, Platform.Messaging, Microsoft.EntityFrameworkCore.Relational, SQL/PostgreSQL providers

**Key principle:** This is the ONLY layer that knows about relational database specifics.

---

### 🚫 Common Violations to AVOID

1. **Adding EF attributes to POCOs in Platform.Messaging**
   ```csharp
   // ❌ WRONG - Violates persistence ignorance
   public class IdempotencyEntry
   {
       [Key]
       [Column("idempotency_id")]
       public Guid Id { get; set; }

       [Required]
       [MaxLength(20)]
       public IdempotencyScope Scope { get; set; }
   }

   // ✅ CORRECT - POCO without attributes
   public class IdempotencyEntry
   {
       public Guid Id { get; set; }
       public IdempotencyScope Scope { get; set; }
       public string Qualifier { get; set; } = string.Empty;
   }
   ```

2. **Using SQL-specific code in Platform.Messaging.EntityFramework**
   ```csharp
   // ❌ WRONG - Couples to SQL Server
   var result = await context.Database.ExecuteSqlRawAsync(
       "SELECT * FROM IdempotencyEntry WHERE Id = {0} AND Scope = {1}", id, scope);

   // ✅ CORRECT - EF Core API (works with any provider)
   var result = await context.Set<IdempotencyEntry>()
       .FirstOrDefaultAsync(e => e.Id == id && e.Scope == scope);
   ```

3. **Hardcoding table names outside configurations**
   ```csharp
   // ❌ WRONG - Should be in Entity Configuration
   modelBuilder.Entity<IdempotencyEntry>().ToTable("IdempotencyEntry");

   // ✅ CORRECT - In Platform.Persistence.EntityFramework.Relational/Configurations/
   internal static class IdempotencyEntryConfiguration
   {
       public static void Configure(this EntityTypeBuilder<IdempotencyEntry> entity)
       {
           entity.ToTable("IdempotencyEntry", "infra");
           entity.HasKey(e => new { e.Id, e.Scope, e.Qualifier });
       }
   }
   ```

4. **Using concrete DbContext in services**
   ```csharp
   // ❌ WRONG - Couples to specific context
   public class IdempotencyService
   {
       private readonly AccountsContext _context;
   }

   // ✅ CORRECT - Generic constraint
   public class IdempotencyEfService<TContext> where TContext : DbContext
   {
       private readonly TContext _context;
   }
   ```

---

### ✅ Validation Checklist

Before committing changes to messaging/persistence layers:

- [ ] **Platform.Messaging** does NOT reference `EntityFrameworkCore`
- [ ] **Platform.Messaging.EntityFramework** does NOT reference SQL/Postgres providers
- [ ] POCOs in **Platform.Messaging** have NO EF attributes
- [ ] Entity Configurations are in **Platform.Persistence.EntityFramework.Relational** ONLY
- [ ] Services in **Platform.Messaging.EntityFramework** use generic `TContext where TContext : DbContext`
- [ ] No SQL-specific code outside **Platform.Persistence.EntityFramework.Relational**

---

### 🎯 When Adding New Features

**Adding new persistence backend (e.g., Cosmos DB):**
- Create `Platform.Persistence.EntityFramework.Cosmos` project
- NO modifications to `Platform.Messaging` (reuse POCOs)
- NO modifications to `Platform.Messaging.EntityFramework` (reuse services)
- Only add Cosmos-specific Entity Configurations

**Adding new idempotency mechanism (e.g., Redis):**
- Create `Platform.Messaging.Redis` project (already exists)
- Implement `IIdempotencyService` interface
- NO modifications to `Platform.Messaging.Abstractions`
- Reuse POCOs from `Platform.Messaging`

---

## Razor Pages and Components
- When modifying Razor pages or components, always consult Radzen documentation first using #radzen_search to ensure compatibility with Radzen Blazor component library best practices and recommendations.

---

## Idempotency - API Response Pattern

When implementing idempotent commands, follow these guidelines to ensure consistent API behavior:

### 1. Always Override `CreateResultForDuplicateRequest()`

**❌ BAD** - Using default value (meaningless for clients):
```csharp
// Base class returns default(TResponse) - e.g., false for bool, null for objects
public virtual Result<TResponse> CreateResultForDuplicateRequest()
{
    return Result.Success(default(TResponse)!);
}
```

**✅ GOOD** - Return meaningful value indicating success:
```csharp
public record CreateOrganizationCommand : IdempotentCommand<bool>
{
    // ... properties and constructor ...

    public override Result<bool> CreateResultForDuplicateRequest()
    {
        return Result.Success(true);  // Organization exists/was created successfully
    }
}
```

### 2. HTTP Response for Duplicate Requests

**Standard**: `200 OK` with same body as original request

**Why**:
- ✅ Maintains idempotency semantics (same status code + same body for 1st and Nth request)
- ✅ Follows industry standards (Stripe, PayPal, AWS, GitHub, Azure)
- ✅ Client doesn't need special logic for duplicates
- ✅ Indicates "operation succeeded" (which is correct - the entity exists)

**Example**:
```csharp
// API Endpoint
public async Task<Results<Ok<bool>, BadRequest<string>>> CreateOrganization(
    [FromHeader(Name = "x-requestid")] Guid requestId,
    CreateOrganizationRequest request,
    [AsParameters] Services services)
{
    var command = new CreateOrganizationCommand(..., requestId: requestId);
    var result = await services.Mediator.Send(command);

    return result.IsSuccess 
        ? TypedResults.Ok(result.Value)      // 200 OK with true
        : TypedResults.BadRequest(result.Error.Message);
}
```

**Response for duplicate**:
```
HTTP/1.1 200 OK
Content-Type: application/json

true
```

### 3. DO NOT Use `409 Conflict` for Duplicates

**❌ WRONG** - Breaks idempotency semantics:
```csharp
if (isDuplicate)
    return TypedResults.Conflict("Request already processed");
```

**Why avoid**:
- ❌ Breaks idempotency: 1st call → 200, 2nd call → 409 (different behavior)
- ❌ Confuses "conflict" (business error) with "duplicate" (expected behavior)
- ❌ Requires special client logic: `if (status == 409) treat_as_success()`
- ❌ Against industry standards

### 4. Advanced: Returning Full DTOs

For commands that return complex objects, consider querying the entity:

```csharp
public record CreateOrganizationCommand : IdempotentCommand<OrganizationDto>
{
    public override Result<OrganizationDto> CreateResultForDuplicateRequest()
    {
        // Query organization by RequestId (if stored as FK)
        var org = _repository.GetByRequestId(RequestId);
        return Result.Success(OrganizationDto.FromEntity(org));
    }
}
```

**Note**: Only implement this if:
- Clients need the full object (not just success indicator)
- RequestId is stored as FK on the entity
- Performance impact is acceptable (database query on duplicate detection)

### 5. Future Enhancement: Idempotency-Replayed Header

**Not implemented yet** - If a service needs to distinguish replayed requests:

```csharp
// Middleware could add header (future):
X-Idempotency-Replayed: true
```

This allows clients to optionally detect duplicates without changing status codes.
**Wait for real use case before implementing** (YAGNI principle).

---

