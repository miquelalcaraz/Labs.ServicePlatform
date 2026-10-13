using Microsoft.EntityFrameworkCore.Storage;

using Miqa.Platform.Messaging.Models;
using Miqa.Platform.Messaging.Options;
using Miqa.Platform.Messaging.Services;
using Miqa.Platform.Persistence;
using Miqa.Platform.Persistence.Configurations;

using Testcontainers.MsSql;

namespace Platform.IntegrationTests.Infrastructure;

public abstract class IntegrationTestBase : IAsyncLifetime
{
    private readonly MsSqlContainer _sqlServerContainer;
    protected TestDbContext DbContext { get; private set; } = null!;

    protected IntegrationTestBase()
    {
        _sqlServerContainer = new MsSqlBuilder()
            .WithImage("mcr.microsoft.com/mssql/server:2022-latest")
            .Build();
    }

    public async Task InitializeAsync()
    {
        await _sqlServerContainer.StartAsync();

        var options = new DbContextOptionsBuilder<TestDbContext>()
            .UseSqlServer(_sqlServerContainer.GetConnectionString())
            .EnableSensitiveDataLogging()
            .Options;

        DbContext = new TestDbContext(options);
        await DbContext.Database.EnsureCreatedAsync();
    }

    public async Task DisposeAsync()
    {
        await DbContext.DisposeAsync();
        await _sqlServerContainer.DisposeAsync();
    }

    protected OutboxMessageService<TestDbContext> CreateOutboxService()
    {
        var options = new OutboxMessageOptions();
        options.RegisterEventTypes(typeof(TestIntegrationEvent));

        return new OutboxMessageService<TestDbContext>(
            DbContext,
            Options.Create(options),
            LoggerFactory.Create(builder => builder.AddConsole())
                .CreateLogger<IOutboxMessageService>());
    }

    protected OutboxMessageService<TestDbContext> CreateIsolatedOutboxService()
    {
        var dbOptions = new DbContextOptionsBuilder<TestDbContext>()
            .UseSqlServer(DbContext.Database.GetConnectionString())
            .Options;
        var isolatedContext = new TestDbContext(dbOptions);

        var options = new OutboxMessageOptions();
        options.RegisterEventTypes(typeof(TestIntegrationEvent));

        return new OutboxMessageService<TestDbContext>(
            isolatedContext,
            Options.Create(options),
            LoggerFactory.Create(builder => builder.AddConsole())
                .CreateLogger<IOutboxMessageService>());
    }

    protected async Task<Guid> SeedOutboxMessageAsync(
        OutboxMessageStatus state = OutboxMessageStatus.NotPublished,
        int timesSent = 0,
        DateTime? creationTime = null)
    {
        var @event = new TestIntegrationEvent { Id = Guid.NewGuid() };
        var message = new OutboxMessage
        {
            Id = @event.Id,
            CreatedAt = creationTime ?? @event.CreationDate,
            TypeName = @event.GetType().FullName!,
            Payload = System.Text.Json.JsonSerializer.Serialize(@event),
            Status = state,
            SendCount = timesSent,
            TransactionId = Guid.NewGuid()
        };

        DbContext.Set<OutboxMessage>().Add(message);

        await DbContext.SaveChangesAsync();
        DbContext.ChangeTracker.Clear();

        return @event.Id;
    }

    protected record TestIntegrationEvent : IntegrationEvent;

    public class TestDbContext : DbContext, ITransactionalUnitOfWork
    {
        private IDbContextTransaction? _currentTransaction;

        public TestDbContext(DbContextOptions<TestDbContext> options) : base(options) { }

        public bool HasActiveTransaction => _currentTransaction is not null;
        public Guid? LastCommittedTransactionId { get; private set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<OutboxMessage>().Configure();
        }

        public async Task<IDbContextTransaction?> BeginTransactionAsync()
        {
            if (_currentTransaction is not null) return null;
            _currentTransaction = await Database.BeginTransactionAsync();
            return _currentTransaction;
        }

        public IDbContextTransaction? UseTransaction(IDbContextTransaction transaction)
        {
            _currentTransaction = Database.UseTransaction(transaction.GetDbTransaction());
            return _currentTransaction;
        }

        public IExecutionStrategy CreateExecutionStrategy()
        {
            return Database.CreateExecutionStrategy();
        }

        public IDbContextTransaction? GetCurrentTransaction()
        {
            return _currentTransaction;
        }

        public async Task CommitTransactionAsync(IDbContextTransaction transaction, CancellationToken cancellationToken = default)
        {
            if (transaction is null) throw new ArgumentNullException(nameof(transaction));
            if (transaction != _currentTransaction)
                throw new InvalidOperationException($"Transaction {transaction.TransactionId} is not current");

            try
            {
                await SaveChangesAsync(cancellationToken);
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

        public int GetPendingChangesCount() => ChangeTracker.Entries()
            .Count(e => e.State == EntityState.Added || e.State == EntityState.Modified || e.State == EntityState.Deleted);
    }
}
