# Unit of Work Pattern

## Executive Summary

### Brief Description
The Unit of Work pattern maintains a list of objects affected by a business transaction and coordinates the writing of changes. It abstracts the persistence mechanism and provides a single point for committing all changes atomically. Implementation uses Entity Framework's DbContext as the unit of work with domain event dispatching integrated.

### Technical Implementation

**Location**:
- Interface: `src/Blocks/Core.Domain/SeedWork/IUnitOfWork.cs`
- Implementation: `src/Blocks/Shared.Persistence.EF/QltDBContextBase.cs`

**Core Components**:

1. **IUnitOfWork Interface**:
```csharp
public interface IUnitOfWork : IDisposable
{
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
    Task<bool> SaveEntitiesAsync(CancellationToken cancellationToken = default);
}
```

2. **QltDBContextBase Implementation**:
```csharp
public abstract class QltDBContextBase : DbContext, IUnitOfWork
{
    private readonly IMediator _mediator;
    private IDbContextTransaction? _currentTransaction;
    
    public bool HasActiveTransaction => _currentTransaction is not null;

    public async Task<bool> SaveEntitiesAsync(CancellationToken cancellationToken = default)
    {
        // Save audit logs if enabled
        if (_configuration.UseAuditLogs)
        {
            await this.SaveAuditLogsAsync(cancellationToken);
        }

        // Persist changes to database
        _ = await base.SaveChangesAsync(cancellationToken);

        // Dispatch domain events AFTER SaveChanges but WITHIN transaction
        await _mediator.DispatchDomainEventsAsync(this, cancellationToken);

        return true;
    }

    public async Task<IDbContextTransaction?> BeginTransactionAsync()
    {
        if (_currentTransaction is not null) return null;

        _currentTransaction = await Database.BeginTransactionAsync();
        return _currentTransaction;
    }

    public async Task CommitTransactionAsync(IDbContextTransaction transaction)
    {
        if (transaction is null) throw new ArgumentNullException(nameof(transaction));
        if (transaction != _currentTransaction) 
            throw new InvalidOperationException($"Transaction {transaction.TransactionId} is not current");

        try
        {
            await SaveChangesAsync();
            await transaction.CommitAsync();
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

    public void RollbackTransaction()
    {
        try
        {
            _currentTransaction?.Rollback();
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
    private readonly IUnitOfWork _unitOfWork;

    public async Task<Result<bool>> Handle(CreateOrganizationCommand request, CancellationToken cancellationToken)
    {
        // Business logic
        var organization = Organization.Create(
            request.Name,
            request.TaxIdNumber,
            // ... other properties
        );

        // Add to repository (in-memory tracking)
        await _repository.AddAsync(organization);

        // Commit changes (persist to database)
        await _unitOfWork.SaveEntitiesAsync(cancellationToken);

        return Result.Success(true);
    }
}
```

5. **Integration with TransactionBehavior**:
```csharp
public class TransactionBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
{
    private readonly QltDBContextBase _dbContext; // IUnitOfWork

    public async Task<TResponse> Handle(...)
    {
        var strategy = _dbContext.Database.CreateExecutionStrategy();

        await strategy.ExecuteAsync(async () =>
        {
            // Begin transaction
            await using var transaction = await _dbContext.BeginTransactionAsync();

            // Execute command handler (calls SaveEntitiesAsync)
            response = await next(cancellationToken);

            // Commit transaction
            await _dbContext.CommitTransactionAsync(transaction);

            transactionId = transaction.TransactionId;
        });

        // Publish outbox events after commit
        if (_integrationEventService != null)
        {
            await _integrationEventService.PublishEventsThroughEventBusAsync(transactionId);
        }

        return response;
    }
}
```

### Critical Points Detected

1. **✅ DbContext as Unit of Work**:
   - Leverages EF's built-in change tracking and transaction management
   - DbContext already implements the unit of work pattern
   - **Benefit**: No need to reinvent the wheel

2. **✅ Domain Event Integration**:
   - `SaveEntitiesAsync()` dispatches domain events after persisting
   - Ensures event handlers see persisted data
   - **Benefit**: Consistent approach to event dispatching

3. **⚠️ SaveChangesAsync vs SaveEntitiesAsync**:
   - Two methods: `SaveChangesAsync()` (from DbContext) and `SaveEntitiesAsync()` (from IUnitOfWork)
   - `SaveEntitiesAsync()` includes domain event dispatching, audit logs
   - `SaveChangesAsync()` does not dispatch events
   - **Risk**: Medium - Confusion about which method to use
   - **Impact**: Medium - If `SaveChangesAsync()` called directly, events not dispatched

4. **⚠️ Domain Events Dispatched Inside Transaction**:
   - Events dispatched AFTER `SaveChanges()` but BEFORE `CommitAsync()`
   - Domain event handlers execute within same transaction
   - **Risk**: HIGH - Handler failure causes rollback (see Domain Events pattern doc)
   - **Impact**: CRITICAL - Inconsistent state if handler fails

5. **⚠️ CommitTransactionAsync Calls SaveChanges Again**:
   - `CommitTransactionAsync()` calls `SaveChanges()` before `CommitAsync()`
   - If called after handler already called `SaveEntitiesAsync()`, saves twice
   - **Risk**: Medium - Performance overhead, potential duplicate events
   - **Impact**: Medium - Inefficiency, possible side effects

6. **✅ Transaction Nesting Prevention**:
   - `BeginTransactionAsync()` returns null if transaction already active
   - Prevents nested transactions
   - **Benefit**: Avoids transaction complexity

7. **⚠️ No Explicit Repository Save**:
   - Repositories don't have `Save()` method
   - All saves go through `IUnitOfWork.SaveEntitiesAsync()`
   - **Risk**: Low - Clear separation of concerns
   - **Impact**: Positive - Enforces unit of work pattern

8. **✅ Retry Strategy Support**:
   - Uses `CreateExecutionStrategy()` for transient failure handling
   - Built-in retry for database connection issues
   - **Benefit**: Resilience against transient failures

9. **⚠️ No SaveChanges Override to Prevent Direct Use**:
   - `SaveChangesAsync()` from DbContext is still callable
   - Developers might accidentally call it instead of `SaveEntitiesAsync()`
   - **Risk**: Medium - Bypasses domain event dispatching
   - **Impact**: Medium - Missing event handlers

10. **✅ Abstract IsUniqueConstraintViolation**:
    - Database-specific constraint violation detection
    - Allows different implementations for SQL Server, PostgreSQL, etc.
    - **Benefit**: Database portability

### Possible Improvements

1. **🔥 HIGH: Prevent Direct SaveChanges Calls**:
```csharp
public abstract class QltDBContextBase : DbContext, IUnitOfWork
{
    // Override to prevent direct calls
    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        throw new InvalidOperationException(
            "Do not call SaveChangesAsync directly. Use SaveEntitiesAsync from IUnitOfWork instead.");
    }

    // Internal method for actual saving
    private async Task<int> InternalSaveChangesAsync(CancellationToken cancellationToken)
    {
        return await base.SaveChangesAsync(cancellationToken);
    }

    public async Task<bool> SaveEntitiesAsync(CancellationToken cancellationToken = default)
    {
        if (_configuration.UseAuditLogs)
            await this.SaveAuditLogsAsync(cancellationToken);

        await InternalSaveChangesAsync(cancellationToken);

        await _mediator.DispatchDomainEventsAsync(this, cancellationToken);

        return true;
    }
}
```

2. **Remove Duplicate SaveChanges from CommitTransactionAsync**:
```csharp
public async Task CommitTransactionAsync(IDbContextTransaction transaction)
{
    if (transaction is null) throw new ArgumentNullException(nameof(transaction));
    if (transaction != _currentTransaction) 
        throw new InvalidOperationException($"Transaction {transaction.TransactionId} is not current");

    try
    {
        // Don't call SaveChangesAsync again - handler already called SaveEntitiesAsync
        // await SaveChangesAsync(); // REMOVE THIS LINE

        await transaction.CommitAsync();
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
```

3. **Add Explicit Method Naming**:
```csharp
public interface IUnitOfWork : IDisposable
{
    // Remove ambiguous SaveChangesAsync
    // Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
    
    // Keep only SaveEntitiesAsync with clear intent
    Task<bool> SaveEntitiesAsync(CancellationToken cancellationToken = default);
    
    // Add explicit transaction methods
    Task<IDbContextTransaction?> BeginTransactionAsync();
    Task CommitTransactionAsync(IDbContextTransaction transaction);
    void RollbackTransaction();
}
```

4. **Add Change Tracking Information**:
```csharp
public interface IUnitOfWork : IDisposable
{
    Task<bool> SaveEntitiesAsync(CancellationToken cancellationToken = default);
    
    // NEW: Query change tracking state
    bool HasChanges();
    int GetPendingChangesCount();
    IEnumerable<object> GetTrackedEntities();
}

// Implementation
public bool HasChanges()
{
    return ChangeTracker.HasChanges();
}

public int GetPendingChangesCount()
{
    return ChangeTracker.Entries()
        .Count(e => e.State == EntityState.Added || 
                    e.State == EntityState.Modified || 
                    e.State == EntityState.Deleted);
}
```

5. **Add Unit of Work Scope**:
```csharp
public interface IUnitOfWorkScope : IAsyncDisposable
{
    Task CommitAsync();
    Task RollbackAsync();
}

public class UnitOfWorkScope : IUnitOfWorkScope
{
    private readonly QltDBContextBase _context;
    private readonly IDbContextTransaction _transaction;

    public UnitOfWorkScope(QltDBContextBase context)
    {
        _context = context;
        _transaction = _context.Database.BeginTransaction();
    }

    public async Task CommitAsync()
    {
        await _context.SaveEntitiesAsync();
        await _transaction.CommitAsync();
    }

    public async Task RollbackAsync()
    {
        await _transaction.RollbackAsync();
    }

    public async ValueTask DisposeAsync()
    {
        await _transaction.DisposeAsync();
    }
}

// Usage:
await using var scope = _unitOfWork.BeginScope();
try
{
    // Business logic
    await _repository.AddAsync(entity);
    
    // Commit all changes
    await scope.CommitAsync();
}
catch
{
    await scope.RollbackAsync();
    throw;
}
```

6. **Add Transaction Isolation Level Support**:
```csharp
public interface IUnitOfWork : IDisposable
{
    Task<IDbContextTransaction?> BeginTransactionAsync(
        IsolationLevel isolationLevel = IsolationLevel.ReadCommitted);
}

public async Task<IDbContextTransaction?> BeginTransactionAsync(
    IsolationLevel isolationLevel = IsolationLevel.ReadCommitted)
{
    if (_currentTransaction is not null) return null;

    _currentTransaction = await Database.BeginTransactionAsync(isolationLevel);
    return _currentTransaction;
}
```

7. **Add SaveAndPublish for Explicit Outbox**:
```csharp
public interface IUnitOfWork : IDisposable
{
    Task<bool> SaveEntitiesAsync(CancellationToken cancellationToken = default);
    Task<bool> SaveAndPublishEventsAsync(CancellationToken cancellationToken = default); // NEW
}

public async Task<bool> SaveAndPublishEventsAsync(CancellationToken cancellationToken = default)
{
    // Save entities and dispatch domain events
    var saved = await SaveEntitiesAsync(cancellationToken);

    // Dispatch integration events from outbox
    if (_integrationEventService != null && _currentTransaction != null)
    {
        await _integrationEventService.PublishEventsThroughEventBusAsync(_currentTransaction.TransactionId);
    }

    return saved;
}
```

8. **Add Audit Trail for Unit of Work**:
```csharp
public class UnitOfWorkAudit
{
    public Guid TransactionId { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string? UserId { get; set; }
    public int EntitiesAffected { get; set; }
    public string? CommandName { get; set; }
    public bool Succeeded { get; set; }
}

// In BeginTransactionAsync:
var audit = new UnitOfWorkAudit
{
    TransactionId = _currentTransaction.TransactionId,
    StartedAt = DateTime.UtcNow,
    UserId = _userContext.UserId,
    CommandName = Activity.Current?.DisplayName
};

// In CommitTransactionAsync:
audit.CompletedAt = DateTime.UtcNow;
audit.EntitiesAffected = ChangeTracker.Entries().Count();
audit.Succeeded = true;

await _auditRepository.SaveAsync(audit);
```

9. **Add Validation Before Save**:
```csharp
public async Task<bool> SaveEntitiesAsync(CancellationToken cancellationToken = default)
{
    // Validate entities before saving
    var validationErrors = ValidateTrackedEntities();
    if (validationErrors.Any())
    {
        throw new DomainValidationException(validationErrors);
    }

    if (_configuration.UseAuditLogs)
        await this.SaveAuditLogsAsync(cancellationToken);

    await base.SaveChangesAsync(cancellationToken);

    await _mediator.DispatchDomainEventsAsync(this, cancellationToken);

    return true;
}

private IEnumerable<ValidationFailure> ValidateTrackedEntities()
{
    var errors = new List<ValidationFailure>();

    var entities = ChangeTracker.Entries()
        .Where(e => e.State == EntityState.Added || e.State == EntityState.Modified)
        .Select(e => e.Entity);

    foreach (var entity in entities)
    {
        // Validate using data annotations, FluentValidation, etc.
        var validationResults = // ... validation logic
        errors.AddRange(validationResults);
    }

    return errors;
}
```

## Conclusion

The Unit of Work implementation correctly leverages Entity Framework's DbContext as the unit of work, avoiding duplication of EF's built-in functionality. Integration with domain events and audit logs is well-designed. However, there are **architectural concerns**:

**Strengths**:
- ✅ Leverages EF's change tracking and transaction management
- ✅ Integrated domain event dispatching
- ✅ Retry strategy support
- ✅ Transaction nesting prevention

**Concerns**:
- ⚠️ Two methods (`SaveChangesAsync`, `SaveEntitiesAsync`) cause confusion
- ⚠️ `CommitTransactionAsync` calls `SaveChanges` again (duplicate save)
- ⚠️ No prevention of direct `SaveChangesAsync` calls (bypasses events)
- 🔴 Domain events dispatched inside transaction (see Domain Events doc)

**Recommended Actions**:
1. 🔥 **HIGH**: Override `SaveChangesAsync()` to throw exception (force use of `SaveEntitiesAsync()`)
2. 🔥 **HIGH**: Remove duplicate `SaveChanges` call from `CommitTransactionAsync()`
3. 🟡 **MEDIUM**: Add change tracking query methods (`HasChanges()`, `GetPendingChangesCount()`)
4. 🟡 **MEDIUM**: Add scoped unit of work for better lifecycle management
5. 🟢 **LOW**: Add isolation level support
6. 🟢 **LOW**: Add unit of work audit trail

**Overall Risk Level**: 🟡 Medium - Works correctly but has confusing API surface

The implementation is functionally correct, but the dual methods (`SaveChangesAsync` vs `SaveEntitiesAsync`) and duplicate save in `CommitTransactionAsync` create confusion and potential bugs. Preventing direct `SaveChangesAsync` calls would enforce proper usage patterns.
