using Microsoft.EntityFrameworkCore.Storage;

using Miqa.Domain.SeedWork;

namespace Miqa.Platform.Persistence
{
    /// <summary>
    /// Extended Unit of Work with explicit transaction management capabilities.
    /// Provides fine-grained control over database transactions beyond the basic UoW pattern.
    /// Extends the base <see cref="IUnitOfWork"/> from the domain layer with infrastructure-specific transaction operations.
    /// </summary>
    public interface ITransactionalUnitOfWork : IUnitOfWork
    {
        /// <summary>
        /// Begins a new database transaction with the default isolation level.
        /// </summary>
        /// <returns>The transaction object, or null if a transaction is already active.</returns>
        Task<IDbContextTransaction?> BeginTransactionAsync();

        /// <summary>
        /// Uses an existing transaction for the current context.
        /// Useful for coordinating transactions across multiple contexts.
        /// </summary>
        /// <param name="transaction">The transaction to use.</param>
        /// <returns>The transaction object that was set.</returns>
        IDbContextTransaction? UseTransaction(IDbContextTransaction transaction);

        /// <summary>
        /// Creates an execution strategy for handling transient failures.
        /// Enables automatic retry logic for database operations.
        /// </summary>
        /// <returns>The execution strategy.</returns>
        IExecutionStrategy CreateExecutionStrategy();

        /// <summary>
        /// Gets the current active transaction, if any.
        /// </summary>
        /// <returns>The current transaction, or null if no transaction is active.</returns>
        IDbContextTransaction? GetCurrentTransaction();

        /// <summary>
        /// Commits the specified transaction, persisting all changes to the database.
        /// </summary>
        /// <param name="transaction">The transaction to commit.</param>
        /// <param name="cancellationToken">A token to observe while waiting for the task to complete.</param>
        Task CommitTransactionAsync(IDbContextTransaction transaction, CancellationToken cancellationToken = default);

        /// <summary>
        /// Rolls back the current transaction, discarding all uncommitted changes.
        /// </summary>
        void RollbackTransaction();

        /// <summary>
        /// Indicates whether there is an active transaction.
        /// </summary>
        bool HasActiveTransaction { get; }

        /// <summary>
        /// Gets the ID of the last successfully committed transaction.
        /// Available after CommitTransactionAsync completes, until the next transaction begins.
        /// </summary>
        Guid? LastCommittedTransactionId { get; }

        /// <summary>
        /// Gets the number of entities in Added, Modified, or Deleted state.
        /// </summary>
        int GetPendingChangesCount();
    }
}
