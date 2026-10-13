# Unit of Work — Parked Improvements

> **Status**: Parked — designs ready to implement when a real use case arises.
> **Related**: [02 — Command Pipeline](../architecture/02-command-pipeline.md), [01 — Messaging & Persistence Layers](../architecture/01-messaging-persistence-layers.md)

These improvements were designed, some prototyped, and intentionally removed following YAGNI. Each section records the rationale and the design so it can be picked up quickly when needed.

---

## 1. Transaction Isolation Level Support

**What**: Allow commands or callers to specify a custom `IsolationLevel` when beginning a transaction.

**Why it was removed**: No command in the current architecture needs anything other than the database provider's default isolation level. Integrating it into `TransactionBehavior` would require a marker interface (e.g., `ITransactionalCommand<IsolationLevel>`) or attribute, which adds design surface with no current consumer.

**Design**:

```csharp
// ITransactionalUnitOfWork
Task<IDbContextTransaction?> BeginTransactionAsync(IsolationLevel isolationLevel);

// MiqaDBContext
public async Task<IDbContextTransaction?> BeginTransactionAsync(IsolationLevel isolationLevel)
{
    if (_currentTransaction is not null) return null;
    _currentTransaction = await Database.BeginTransactionAsync(isolationLevel);
    return _currentTransaction;
}
```

**To integrate into TransactionBehavior**, a command would declare its isolation level:

```csharp
// Option A: Marker interface
public interface IRequiresIsolation
{
    IsolationLevel IsolationLevel { get; }
}

// Option B: Attribute
[TransactionIsolation(IsolationLevel.Serializable)]
public record TransferFundsCommand : ICommand<bool> { ... }
```

---

## 2. Scoped Unit of Work (`IUnitOfWorkScope`)

**What**: A disposable scope that wraps begin/commit/rollback into a single `IAsyncDisposable` object with automatic rollback on disposal if not committed.

**Why it was removed**: The pipeline handles transactions via `TransactionBehavior`. No background service or ad-hoc code in the current architecture needs manual transaction management. The scope's throw-on-active-transaction semantics are incompatible with `TransactionBehavior`'s graceful skip pattern.

**Design**:

```csharp
public interface IUnitOfWorkScope : IAsyncDisposable
{
    Guid TransactionId { get; }
    Task CommitAsync(CancellationToken cancellationToken = default);
    void Rollback();
}

internal sealed class UnitOfWorkScope : IUnitOfWorkScope
{
    private readonly ITransactionalUnitOfWork _unitOfWork;
    private readonly IDbContextTransaction _transaction;
    private bool _completed;

    internal UnitOfWorkScope(ITransactionalUnitOfWork unitOfWork, IDbContextTransaction transaction)
    {
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _transaction = transaction ?? throw new ArgumentNullException(nameof(transaction));
    }

    public Guid TransactionId => _transaction.TransactionId;

    public async Task CommitAsync(CancellationToken cancellationToken = default)
    {
        await _unitOfWork.CommitTransactionAsync(_transaction, cancellationToken);
        _completed = true;
    }

    public void Rollback()
    {
        if (!_completed)
        {
            _unitOfWork.RollbackTransaction();
            _completed = true;
        }
    }

    public ValueTask DisposeAsync()
    {
        if (!_completed)
            _unitOfWork.RollbackTransaction();

        return ValueTask.CompletedTask;
    }
}

public static class UnitOfWorkScopeExtensions
{
    public static async Task<IUnitOfWorkScope> BeginScopeAsync(this ITransactionalUnitOfWork unitOfWork)
    {
        var transaction = await unitOfWork.BeginTransactionAsync()
            ?? throw new InvalidOperationException("Cannot create scope: a transaction is already active.");

        return new UnitOfWorkScope(unitOfWork, transaction);
    }
}
```

**Usage** (for background services, data migrations, seed logic):

```csharp
await using var scope = await unitOfWork.BeginScopeAsync();
await _repository.AddAsync(entity);
await scope.CommitAsync();
// Auto-rollback on disposal if not committed
```

**When to implement**: When a background service or non-pipeline code needs explicit transaction management.

---

## 3. Change Tracker Inspection (`HasChanges()`)

**What**: Expose `ChangeTracker.HasChanges()` on `ITransactionalUnitOfWork`.

**Why it was removed**: Redundant with `GetPendingChangesCount() > 0`. While `HasChanges()` short-circuits (more performant for a boolean check), no code path needs a "has changes?" boolean — `GetPendingChangesCount()` serves the telemetry use case, and skipping commit based on `HasChanges()` would be a risky micro-optimization.

**Design**:

```csharp
// ITransactionalUnitOfWork
bool HasChanges();

// MiqaDBContext
public bool HasChanges() => ChangeTracker.HasChanges();
```

**When to implement**: If a performance-critical path needs a quick boolean check without counting all tracked entities.

---

## 4. Unit of Work Audit Trail

**What**: A dedicated database table recording transaction metadata (TransactionId, StartedAt, CompletedAt, UserId, EntitiesAffected, CommandName, Succeeded).

**Why it was deferred**: `TransactionBehavior` already records `transaction.id`, `transaction.duration_ms`, `transaction.success` and `transaction.entities_affected` as activity tags and histogram metrics (see [06 — Telemetry](../architecture/06-telemetry.md)). A separate table would duplicate this with higher write overhead.

**When to implement**: If compliance or regulatory requirements demand a persistent, queryable audit trail that outlives telemetry retention, or if OpenTelemetry infrastructure is not available in a deployment. Entity-level auditing is covered separately in the [Audit Log proposal](audit-log-proposal.md).

---

## 5. Validation Before Save

**What**: Validate tracked entities (via DataAnnotations or FluentValidation) inside `SaveChangesAsync()` before flushing to the database.

**Why it was deferred**: `ValidationBehavior` in the pipeline validates commands BEFORE handler execution. Adding entity-level validation at save time would create a second validation layer with different error semantics (exception vs. `Result`).

**When to implement**: If aggregate invariants need to be enforced at the persistence boundary (defense-in-depth), or if entities are modified by domain event handlers and need re-validation before commit.

---

## Summary

| Improvement | Not implemented because | Trigger to implement |
|------------|----------------|---------------------|
| IsolationLevel | No command needs it | Financial transactions, read-heavy queries |
| UnitOfWorkScope | Pipeline handles it | Background service needs transactions |
| HasChanges() | Redundant with count | Performance-critical boolean check |
| Audit Trail | OpenTelemetry covers it | Compliance/regulatory requirement |
| Validation Before Save | Pipeline validates commands | Defense-in-depth for aggregate invariants |
