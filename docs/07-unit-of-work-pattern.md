# Unit of Work Pattern

## Executive Summary

### Brief Description
The Unit of Work pattern maintains a list of objects affected by a business transaction and coordinates the writing of changes. It abstracts the persistence mechanism and provides a single point for committing all changes atomically. Implementation uses Entity Framework's DbContext as the unit of work with domain event dispatching integrated.

### Technical Implementation

**Location**:
- Base Interface: `src/Qlt.Domain/Qlt.Domain.Core/SeedWork/IUnitOfWork.cs`
- Extended Interface: `src/Qlt.Platform/Qlt.Platform.Persistence.EntityFramework/ITransactionalUnitOfWork.cs`
- Implementation: `src/Qlt.Platform/Qlt.Platform.Persistence.EntityFramework.Relational/QltDBContext.cs`

**Core Components**:

1. **IUnitOfWork Interface** (Domain layer — minimal contract):
```csharp
public interface IUnitOfWork : IDisposable
{
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
```

1b. **ITransactionalUnitOfWork Interface** (Platform layer — full transaction management):
```csharp
public interface ITransactionalUnitOfWork : IUnitOfWork
{
    Task<IDbContextTransaction?> BeginTransactionAsync();
    IDbContextTransaction? UseTransaction(IDbContextTransaction transaction);
    IExecutionStrategy CreateExecutionStrategy();
    IDbContextTransaction? GetCurrentTransaction();
    Task CommitTransactionAsync(IDbContextTransaction transaction, CancellationToken cancellationToken = default);
    void RollbackTransaction();
    bool HasActiveTransaction { get; }
    Guid? LastCommittedTransactionId { get; }
    int GetPendingChangesCount();
}
```

2. **QltDBContext Implementation** (current):
```csharp
public class QltDBContext : DbContext, ITransactionalUnitOfWork, IDomainEntityChangeTracker
{
    private IDbContextTransaction? _currentTransaction = null!;
    public bool HasActiveTransaction => _currentTransaction is not null;
    public Guid? LastCommittedTransactionId { get; private set; }
    protected readonly QltDbContextConfiguration _configuration;

    public async Task<IDbContextTransaction?> BeginTransactionAsync()
    {
        if (_currentTransaction is not null) return null;
        _currentTransaction = await Database.BeginTransactionAsync();
        return _currentTransaction;
    }

    public async Task CommitTransactionAsync(IDbContextTransaction transaction, CancellationToken cancellationToken = default)
    {
        if (transaction is null) throw new ArgumentNullException(nameof(transaction));
        if (transaction != _currentTransaction)
            throw new InvalidOperationException($"Transaction {transaction.TransactionId} is not current");

        try
        {
            await base.SaveChangesAsync(cancellationToken);

            if (_configuration.UseAuditLogs)
                await this.SaveAuditLogsAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            LastCommittedTransactionId = transaction.TransactionId;
        }
        catch
        {
            RollbackTransaction();
            throw;
        }
        finally
        {
            if (_currentTransaction is not null)
            {
                _currentTransaction.Dispose();
                _currentTransaction = null;
            }
        }
    }

    public void RollbackTransaction() { ... }

    public int GetPendingChangesCount() =>
        ChangeTracker.Entries()
            .Count(e => e.State == EntityState.Added ||
                        e.State == EntityState.Modified ||
                        e.State == EntityState.Deleted);
}
```

3. **Configuration Support**:
```csharp
protected readonly QltDbContextConfiguration _configuration;

protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    // Apply entity configurations from registered assemblies
    foreach (var assembly in _configuration.AssembliesToRegister)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(assembly);
    }
    
    // Apply default schema
    if (!string.IsNullOrEmpty(_configuration.DefaultSchema))
    {
        modelBuilder.HasDefaultSchema(_configuration.DefaultSchema);
    }
}
```

4. **Example Usage in Command Handler**:
```csharp
public class CreateOrganizationCommandHandler : ICommandHandler<CreateOrganizationCommand, bool>
{
    private readonly IOrganizationRepository _repository;

    public async Task<Result<bool>> Handle(CreateOrganizationCommand request, CancellationToken cancellationToken)
    {
        var organization = Organization.Create(
            request.Name,
            request.TaxIdNumber,
            // ... other properties
        );

        await _repository.AddAsync(organization);

        // Save changes (flushes to DB within active transaction)
        await _repository.UnitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(true);
    }
}
```
> **Note**: `SaveChangesAsync()` flushes entity changes to the database within the active transaction. The commit is handled by `TransactionBehavior` in the pipeline. Domain event dispatch is handled by `DomainEventProcessorBehavior`.

5. **Integration with TransactionBehavior**:
```csharp
public class TransactionBehavior<TRequest, TResponse, TContext> : IPipelineBehavior<TRequest, TResponse>
    where TContext : DbContext
{
    private readonly TContext _dbContext; // ITransactionalUnitOfWork

    public async Task<TResponse> Handle(...)
    {
        var strategy = ((ITransactionalUnitOfWork)_dbContext).CreateExecutionStrategy();

        await strategy.ExecuteAsync(async () =>
        {
            // Begin transaction
            await using var transaction = await _dbContext.BeginTransactionAsync();

            // Execute command handler (calls SaveChangesAsync within transaction)
            response = await next(cancellationToken);

            // Commit transaction (final flush + audit logs + commit)
            await _dbContext.CommitTransactionAsync(transaction);
        });

        return response;
    }
}
```
> **Note**: Integration event publishing is handled by `IntegrationEventProcessorBehavior` which reads `LastCommittedTransactionId` after commit. See [doc 17](17-developer-guide-transactional-pipeline.md).

### Critical Points Detected

1. **✅ DbContext as Unit of Work**:
   - Leverages EF's built-in change tracking and transaction management
   - DbContext already implements the unit of work pattern
   - **Benefit**: No need to reinvent the wheel

2. **✅ Domain Event Integration**:
   - Domain events dispatched by `DomainEventProcessorBehavior` after handler execution, within the transaction
   - Ensures event handlers participate in the same atomic unit
   - **Benefit**: Consistent approach to event dispatching via pipeline behaviors

3. **✅ Single Save Method**:
   - Only `SaveChangesAsync()` exists on `IUnitOfWork` (no `SaveEntitiesAsync`)
   - Domain event dispatching is handled by `DomainEventProcessorBehavior` in the pipeline
   - Audit logs are handled by `CommitTransactionAsync()` before commit
   - **Benefit**: No confusion — single method, responsibilities separated into pipeline behaviors

4. **✅ Design Decision: Domain Events Dispatched Within Transaction**:
   - Events dispatched AFTER `SaveChanges()` but BEFORE `CommitAsync()` — this is intentional
   - Domain event handlers execute within the same transaction (atomic consistency)
   - Based on eShop reference architecture and *Patterns of Enterprise Application Architecture*
   - **Benefit**: Handler failure causes full rollback (aggregate + outbox + idempotency)
   - **Trade-off**: Transaction duration increases with each handler. Keep handlers lightweight.
   - See [doc 17](17-developer-guide-transactional-pipeline.md) for developer considerations

5. **✅ CommitTransactionAsync Final Flush**:
   - `CommitTransactionAsync()` calls `SaveChangesAsync()` before `CommitAsync()` — this is intentional
   - This final flush captures any changes made by domain event handlers during `DomainEventProcessorBehavior`
   - Followed by audit logs (if enabled), then atomic commit
   - **Benefit**: Ensures all side effects (handler + domain events + audit) are persisted atomically

6. **✅ Transaction Nesting Prevention**:
   - `BeginTransactionAsync()` returns null if transaction already active
   - Prevents nested transactions
   - **Benefit**: Avoids transaction complexity

7. **✅ No Explicit Repository Save**:
   - Repositories don't have `Save()` method
   - All saves go through `IUnitOfWork.SaveChangesAsync()`
   - **Risk**: Low - Clear separation of concerns
   - **Impact**: Positive - Enforces unit of work pattern

8. **✅ Retry Strategy Support**:
   - Uses `CreateExecutionStrategy()` for transient failure handling
   - Built-in retry for database connection issues
   - **Benefit**: Resilience against transient failures

9. **✅ SaveChangesAsync is the Correct Method**:
   - `SaveChangesAsync()` is the ONLY save method (no `SaveEntitiesAsync`)
   - Domain event dispatching is handled by `DomainEventProcessorBehavior` in the pipeline
   - Calling `SaveChangesAsync()` within a transaction flushes changes without committing
   - **Benefit**: Clear, single method — pipeline handles cross-cutting concerns

10. **✅ Abstract IsUniqueConstraintViolation**:
    - Database-specific constraint violation detection
    - Allows different implementations for SQL Server, PostgreSQL, etc.
    - **Benefit**: Database portability

### Possible Improvements

> **Status Legend**: ✅ = Implemented | 🟡 = Future consideration | ❌ = Rejected/Superseded

#### ✅ Implemented

1. **~~Prevent Direct SaveChanges Calls~~** → **Superseded by architecture**:
   - `SaveEntitiesAsync` was removed. Only `SaveChangesAsync()` exists.
   - Domain event dispatching is handled by `DomainEventProcessorBehavior` (not by the save method).
   - `CommitTransactionAsync` performs a final flush + audit + commit atomically.
   - **Status**: ❌ No longer applicable — single save method + pipeline behaviors eliminate the confusion.

2. **~~Remove Duplicate SaveChanges from CommitTransactionAsync~~** → **Resolved (intentional design)**:
   - The `SaveChangesAsync()` call in `CommitTransactionAsync` is intentional.
   - It flushes changes made by domain event handlers during `DomainEventProcessorBehavior`.
   - **Status**: ✅ Design verified and documented — not a bug, it's required for atomic commit.

3. **~~Add Explicit Method Naming~~** → **Superseded**:
   - The current architecture uses a single `SaveChangesAsync()` on `IUnitOfWork` (domain layer).
   - Transaction methods live on `ITransactionalUnitOfWork` (infrastructure layer).
   - **Status**: ✅ Clean separation achieved — domain has minimal contract, infrastructure has full capabilities.

4. **~~Add Change Tracking Information~~** → **Partially implemented (YAGNI applied)**:
   - `GetPendingChangesCount()` — ✅ Kept and integrated into `TransactionBehavior` telemetry (`transaction.entities_affected`)
   - `HasChanges()` — 🗑️ Removed (redundant with `GetPendingChangesCount() > 0`, no production use case)
   - `GetTrackedEntities()` — Not added (`IDomainEntityChangeTracker.GetEntitiesWithChanges()` covers domain entities)

5. **~~Add Unit of Work Scope~~** → **🗑️ Removed (YAGNI)**:
   - The pipeline handles transactions via `TransactionBehavior`. No background service or ad-hoc code needed manual scope management.
   - The scope's throw-on-active-transaction semantics were incompatible with `TransactionBehavior`'s graceful skip pattern.
   - Design preserved in [doc 18](18-uow-future-improvements.md) for future use.

6. **~~Add Transaction Isolation Level Support~~** → **🗑️ Removed (YAGNI)**:
   - No command needs anything other than the provider's default isolation level.
   - Integrating into `TransactionBehavior` requires a command marker interface — no current design for this.
   - Design preserved in [doc 18](18-uow-future-improvements.md) for future use.

#### 🟡 Future Considerations

7. **SaveAndPublish for Explicit Outbox** (future):
   - Integration event publishing is handled by `IntegrationEventProcessorBehavior` after commit.
   - No need for explicit `SaveAndPublishEventsAsync()` — the pipeline handles it automatically.
   - **Status**: 🟡 Not implemented — pipeline architecture makes this unnecessary for now.

8. **Audit Trail for Unit of Work** (future):
   - OpenTelemetry `BehaviorHelper` already provides transaction-level telemetry via activities and metrics.
   - Tags include: `transaction.id`, `transaction.duration_ms`, `transaction.success`, `transaction.entities_affected`.
   - A dedicated DB table for audit trail is a future consideration if OpenTelemetry is insufficient.
   - **Status**: 🟡 Deferred — covered by existing OpenTelemetry infrastructure.

9. **Validation Before Save** (future):
   - Command validation is handled by `ValidationBehavior` in the pipeline (before handler execution).
   - Entity-level validation before save would add a second validation layer.
   - **Status**: 🟡 Not implemented — pipeline validation is sufficient for current needs.

> **All removed designs are preserved in [doc 18 — UoW Future Improvements](18-uow-future-improvements.md)** for quick implementation when a real use case arises.

## Conclusion

The Unit of Work implementation correctly leverages Entity Framework's DbContext as the unit of work, avoiding duplication of EF's built-in functionality. Integration with domain events and audit logs is well-designed.

**Strengths**:
- ✅ Leverages EF's change tracking and transaction management
- ✅ Domain event dispatching via `DomainEventProcessorBehavior` pipeline behavior
- ✅ Retry strategy support
- ✅ Transaction nesting prevention
- ✅ `GetPendingChangesCount()` integrated into `TransactionBehavior` telemetry

**Design Decisions**:
- ✅ Domain events dispatched within the same transaction — intentional (see [doc 03](03-domain-events-pattern.md) and [doc 17](17-developer-guide-transactional-pipeline.md))
- ✅ `CommitTransactionAsync` calls `SaveChangesAsync` before `CommitAsync` — flushes outbox messages and handler modifications added during domain event dispatch

**Considerations**:
- ⚠️ `SaveChangesAsync()` in command handlers flushes to DB but does NOT commit. Developers must understand this distinction.
- ⚠️ Keep domain event handlers lightweight to minimize transaction duration

**Telemetry** (via `BehaviorHelper` + OpenTelemetry):
- `transaction.id` — Transaction correlation
- `transaction.duration_ms` — Performance monitoring
- `transaction.success` — Success/failure tracking
- `transaction.entities_affected` — Transaction weight (uses `GetPendingChangesCount()`)

**YAGNI Applied**:
- 🗑️ `HasChanges()` — Removed (redundant)
- 🗑️ `IUnitOfWorkScope` — Removed (pipeline handles transactions)
- 🗑️ `BeginTransactionAsync(IsolationLevel)` — Removed (no consumer)
- 📄 All designs preserved in [doc 18](18-uow-future-improvements.md)

**Overall Risk Level**: 🟢 Excellent — Clean, focused implementation with no dead code. Every member on the interface has a real consumer.
