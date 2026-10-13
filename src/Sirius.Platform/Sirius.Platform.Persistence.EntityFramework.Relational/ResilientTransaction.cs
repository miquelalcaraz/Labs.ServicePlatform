using Microsoft.EntityFrameworkCore;

namespace Sirius.Platform.Persistence;

/// <summary>
/// Helper class for executing actions within a resilient database transaction.
/// Uses EF Core's execution strategy to handle transient failures.
/// </summary>
public class ResilientTransaction
{
    private readonly DbContext _context;

    private ResilientTransaction(DbContext context) =>
        _context = context ?? throw new ArgumentNullException(nameof(context));

    public static ResilientTransaction New(DbContext context) => new(context);

    /// <summary>
    /// Executes an action within a resilient transaction.
    /// Uses EF Core's execution strategy to retry on transient failures.
    /// </summary>
    /// <param name="action">The action to execute within the transaction</param>
    /// <remarks>
    /// See: https://docs.microsoft.com/en-us/ef/core/miscellaneous/connection-resiliency
    /// </remarks>
    public async Task ExecuteAsync(Func<Task> action)
    {
        var strategy = _context.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await _context.Database.BeginTransactionAsync();
            await action();
            await transaction.CommitAsync();
        });
    }
}
