using MediatR;

using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

using Npgsql;

using Qlt.Domain;
using Qlt.Domain.SeedWork;
using Qlt.Platform.Messaging;
using Qlt.Platform.Persistence.Audit;
using Qlt.Platform.Persistence.Configurations;
using Qlt.Platform.Persistence.Exceptions;
using Qlt.Platform.Persistence.Extensions;

namespace Qlt.Platform.Persistence;

/// <summary>
/// Base DbContext for all Qlt contexts. 
/// Provides common functionality like domain events dispatching, audit logging, and transaction management.
/// </summary>
public class QltDBContext : DbContext, ITransactionalUnitOfWork, IDomainEventCollector
{
    private readonly IMediator _mediator = null!;
    private IDbContextTransaction? _currentTransaction = null!;
    private List<Entity>? _entitiesWithEvents;

    public bool HasActiveTransaction => _currentTransaction is not null;
    public Guid? LastCommittedTransactionId { get; private set; }

    protected readonly QltDbContextConfiguration _configuration;

    public QltDBContext(DbContextOptions options, IMediator mediator, QltDbContextConfiguration configuration) : base(options)
    {
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
    }

    public IDbContextTransaction? GetCurrentTransaction()
    {
        return _currentTransaction;
    }
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

        if (_configuration.UseAuditLogs)
        {
            modelBuilder.ConfigureAuditLogs();
        }
        if (_configuration.UseIdempotency)
        {
            modelBuilder.ConfigureIdempotencyEntry();
        }
        if (_configuration.UseTransactionalOutbox)
        {
            modelBuilder.ConfigureTransactionalOutbox();
        }
    }
    public async Task<bool> SaveEntitiesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            if (_configuration.UseAuditLogs)
            {
                await this.SaveAuditLogsAsync(cancellationToken);
            }

            _entitiesWithEvents = ChangeTracker
                .Entries<Entity>()
                .Where(x => x.Entity.DomainEvents != null && x.Entity.DomainEvents.Any())
                .Select(x => x.Entity)
                .ToList();

            await base.SaveChangesAsync(cancellationToken);

            return true;
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
        if (_currentTransaction is not null) return null;

        _currentTransaction = await Database.BeginTransactionAsync();
        return _currentTransaction;
    }

    public async Task CommitTransactionAsync(IDbContextTransaction transaction)
    {
        if (transaction is null) throw new ArgumentNullException(nameof(transaction));
        if (transaction != _currentTransaction) throw new InvalidOperationException($"Transaction {transaction.TransactionId} is not current");

        try
        {
            await transaction.CommitAsync();
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

    public IReadOnlyList<Entity> GetEntitiesWithPendingEvents()
    {
        var entities = _entitiesWithEvents ?? new List<Entity>();
        _entitiesWithEvents = null;
        return entities;
    }

}
