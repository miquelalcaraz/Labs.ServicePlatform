using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

using Npgsql;

using Miqa.Domain.SeedWork;
using Miqa.Platform.Persistence.Configurations;
using Miqa.Platform.Persistence.Exceptions;
using Miqa.Platform.Persistence.Options;

namespace Miqa.Platform.Persistence;

/// <summary>
/// Base DbContext for all Miqa contexts. 
/// Provides common functionality like domain events dispatching and transaction management.
/// </summary>
public class MiqaDBContext : DbContext, ITransactionalUnitOfWork, IDomainEntityChangeTracker
{
    public bool HasActiveTransaction => Database.CurrentTransaction is not null;
    public Guid? LastCommittedTransactionId { get; private set; }

    protected readonly MiqaDbContextConfiguration _configuration;

    public MiqaDBContext(DbContextOptions options, MiqaDbContextConfiguration configuration) : base(options)
    {
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
    }

    public IDbContextTransaction? GetCurrentTransaction() => Database.CurrentTransaction;

    public IDbContextTransaction? UseTransaction(IDbContextTransaction transaction)
    {
        return Database.UseTransaction(transaction.GetDbTransaction());
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        foreach (var assembly in _configuration.AssembliesToRegister)
        {
            modelBuilder.ApplyConfigurationsFromAssembly(assembly);
        }
        if (!string.IsNullOrEmpty(_configuration.DefaultSchema))
        {
            modelBuilder.HasDefaultSchema(_configuration.DefaultSchema);
        }

        if (_configuration.UseIdempotency)
        {
            modelBuilder.ConfigureIdempotencyEntry();
        }
        if (_configuration.UseTransactionalOutbox)
        {
            modelBuilder.ConfigureTransactionalOutbox();
        }

        modelBuilder.ConfigureEntityAuditProperties();
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        ApplyAuditTimestamps();

        try
        {
            return await base.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex)
        {
            if (IsUniqueConstraintViolation(ex))
            {
                throw new UniqueConstraintViolationException(
                    "A database unique constraint violation occurred. This is an exceptional case that indicates either: " +
                    "(1) a race condition bypassed proactive idempotency checks, or " +
                    "(2) a business rule violation (duplicate key on entities like TaxId, Email, etc.). " +
                    "Note: Normal idempotency flow uses proactive checks and should NOT rely on this exception.",
                    ex);
            }
            throw;
        }
    }


    public IExecutionStrategy CreateExecutionStrategy()
    {
        IExecutionStrategy executionStrategy = Database.CreateExecutionStrategy();
        return executionStrategy;
    }

    public async Task<IDbContextTransaction?> BeginTransactionAsync()
    {
        if (Database.CurrentTransaction is not null) return null;

        return await Database.BeginTransactionAsync();
    }

    public async Task CommitTransactionAsync(IDbContextTransaction transaction, CancellationToken cancellationToken = default)
    {
        if (transaction is null) throw new ArgumentNullException(nameof(transaction));
        if (transaction != Database.CurrentTransaction) throw new InvalidOperationException($"Transaction {transaction.TransactionId} is not current");

        try
        {
            ApplyAuditTimestamps();
            await base.SaveChangesAsync(cancellationToken);
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
            transaction.Dispose();
        }
    }

    public void RollbackTransaction()
    {
        var transaction = Database.CurrentTransaction;
        try
        {
            transaction?.Rollback();
        }
        finally
        {
            transaction?.Dispose();
        }
    }

    public int GetPendingChangesCount() =>
        ChangeTracker.Entries()
            .Count(e => e.State == EntityState.Added ||
                        e.State == EntityState.Modified ||
                        e.State == EntityState.Deleted);

    private bool IsUniqueConstraintViolation(DbUpdateException ex)
    {
        if (IsSqlServeUniqueConstraintViolation(ex))
        {
            return true;
        }
        if (IsPostgresUniqueConstraintViolation(ex))
        {
            return true;
        }
        if (IsGenericUniqueConstraintViolation(ex))
        {
            return true;
        }
        return false;
    }

    private bool IsSqlServeUniqueConstraintViolation(DbUpdateException ex)
    {
        return ex.InnerException is SqlException sqlEx
            && (sqlEx.Number == 2627 || sqlEx.Number == 2601);
    }

    private bool IsPostgresUniqueConstraintViolation(DbUpdateException ex)
    {
        return ex.InnerException is PostgresException pgEx
            && pgEx.SqlState == "23505"; // unique_violation
    }

    private bool IsGenericUniqueConstraintViolation(DbUpdateException ex)
    {
        // Check the inner exception message for unique constraint violation keywords
        var innerMessage = ex.InnerException?.Message ?? string.Empty;

        return innerMessage.Contains("duplicate", StringComparison.OrdinalIgnoreCase)
            || innerMessage.Contains("unique", StringComparison.OrdinalIgnoreCase)
            || innerMessage.Contains("constraint", StringComparison.OrdinalIgnoreCase)
            || innerMessage.Contains("PRIMARY KEY", StringComparison.OrdinalIgnoreCase)
            || innerMessage.Contains("UNIQUE KEY", StringComparison.OrdinalIgnoreCase);
    }

    public IReadOnlyList<Entity> GetEntitiesWithChanges()
    {
        return ChangeTracker.Entries<Entity>()
            .Select(x => x.Entity)
            .ToList();
    }

    private void ApplyAuditTimestamps()
    {
        var now = DateTimeOffset.UtcNow;

        foreach (var entry in ChangeTracker.Entries<Entity>())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    entry.Property(nameof(Entity.CreatedAt)).CurrentValue = now;
                    break;

                case EntityState.Modified:
                    entry.Property(nameof(Entity.UpdatedAt)).CurrentValue = now;
                    entry.Property(nameof(Entity.CreatedAt)).IsModified = false;
                    break;
            }
        }
    }

}
