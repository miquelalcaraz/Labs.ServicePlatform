using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Sirius.Platform.Messaging.Builders;
using Sirius.Platform.Messaging.IntegrationEvents;
using Sirius.Platform.Messaging.Models;
using Sirius.Platform.Messaging.Services;
using Sirius.Platform.Persistence;

using StackExchange.Redis;

namespace Platform.UnitTests.Messaging.EntityFramework;

public class EfIdempotencyBuilderTests : IDisposable
{
    private readonly ServiceCollection _services;
    private readonly BuilderTestDbContext _context;
    private readonly Mock<IConnectionMultiplexer> _mockRedis;

    public EfIdempotencyBuilderTests()
    {
        var options = new DbContextOptionsBuilder<BuilderTestDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new BuilderTestDbContext(options);
        _mockRedis = new Mock<IConnectionMultiplexer>();
        _mockRedis.Setup(r => r.GetDatabase(It.IsAny<int>(), It.IsAny<object>()))
            .Returns(Mock.Of<StackExchange.Redis.IDatabase>());

        _services = new ServiceCollection();
        _services.AddSingleton(_context);
        _services.AddScoped<BuilderTestDbContext>(_ => _context);
        _services.AddSingleton<IConnectionMultiplexer>(_mockRedis.Object);
        _services.AddLogging();
    }

    [Fact]
    public void ForIntegrationEventHandlers_UnwrapsRedisDecorator_WhenBothRegistered()
    {
        _services.AddScoped<IIntegrationEventHandler<BuilderTestEvent>, BuilderTestEventHandler>();

        _services.AddRedisIdempotencyCache(opts =>
        {
            opts.ProcessingTtl = TimeSpan.FromSeconds(30);
            opts.CompletedTtl = TimeSpan.FromMinutes(5);
        }).ForIntegrationEventHandlers();

        _services.AddIdempotencyEntityFramework<BuilderTestDbContext>()
            .ForIntegrationEventHandlers();

        var provider = _services.BuildServiceProvider();
        var handler = provider.GetRequiredService<IIntegrationEventHandler<BuilderTestEvent>>();

        handler.Should().BeOfType<IdempotencyIntegrationEventEfDecorator<BuilderTestEvent>>();

        var efDecorator = (IDecoratedHandler<BuilderTestEvent>)handler;
        efDecorator.InnerHandler.Should().BeOfType<BuilderTestEventHandler>(
            "the Redis decorator should be unwrapped, leaving the concrete handler as inner");
    }

    [Fact]
    public void ForIntegrationEventHandlers_WithoutRedis_WrapsHandlerDirectly()
    {
        _services.AddScoped<IIntegrationEventHandler<BuilderTestEvent>, BuilderTestEventHandler>();

        _services.AddIdempotencyEntityFramework<BuilderTestDbContext>()
            .ForApiRequests()
            .ForIntegrationEventHandlers();

        var provider = _services.BuildServiceProvider();
        var handler = provider.GetRequiredService<IIntegrationEventHandler<BuilderTestEvent>>();

        handler.Should().BeOfType<IdempotencyIntegrationEventEfDecorator<BuilderTestEvent>>();

        var efDecorator = (IDecoratedHandler<BuilderTestEvent>)handler;
        efDecorator.InnerHandler.Should().BeOfType<BuilderTestEventHandler>();
    }

    [Fact]
    public void ForApiRequests_WithRedisRegistered_DecoratesIdempotencyService()
    {
        _services.AddRedisIdempotencyCache(opts =>
        {
            opts.ProcessingTtl = TimeSpan.FromSeconds(30);
        }).ForApiRequests();

        _services.AddIdempotencyEntityFramework<BuilderTestDbContext>()
            .ForApiRequests();

        var provider = _services.BuildServiceProvider();
        var service = provider.GetRequiredService<IIdempotencyService>();

        service.Should().BeOfType<IdempotencyEfService<BuilderTestDbContext>>(
            "EF should wrap the existing Redis service");
    }

    [Fact]
    public void ForApiRequests_WithoutRedis_RegistersStandaloneEfService()
    {
        _services.AddIdempotencyEntityFramework<BuilderTestDbContext>()
            .ForApiRequests();

        var provider = _services.BuildServiceProvider();
        var service = provider.GetRequiredService<IIdempotencyService>();

        service.Should().BeOfType<IdempotencyEfService<BuilderTestDbContext>>();
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }
}

public record BuilderTestEvent : IntegrationEvent;

public class BuilderTestEventHandler : IIntegrationEventHandler<BuilderTestEvent>
{
    public Task Handle(BuilderTestEvent @event) => Task.CompletedTask;
}

public class BuilderTestDbContext : DbContext, ITransactionalUnitOfWork
{
    public BuilderTestDbContext(DbContextOptions<BuilderTestDbContext> options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<IdempotencyEntry>(entity =>
        {
            entity.HasKey(e => new { e.Id, e.Scope, e.Qualifier });
            entity.Property(e => e.Scope).HasConversion<string>();
            entity.Property(e => e.Qualifier).IsRequired();
            entity.Property(e => e.ProcessedAt).IsRequired();
        });
    }

    public bool HasActiveTransaction => Database.CurrentTransaction != null;
    public Guid? LastCommittedTransactionId { get; private set; }
    public IExecutionStrategy CreateExecutionStrategy() => Database.CreateExecutionStrategy();
    public Task<IDbContextTransaction?> BeginTransactionAsync() => Database.BeginTransactionAsync()!;
    public async Task CommitTransactionAsync(IDbContextTransaction transaction, CancellationToken cancellationToken = default)
    {
        await transaction.CommitAsync(cancellationToken);
        LastCommittedTransactionId = transaction.TransactionId;
    }
    public IDbContextTransaction? GetCurrentTransaction() => Database.CurrentTransaction;
    public IDbContextTransaction? UseTransaction(IDbContextTransaction transaction)
    {
        Database.UseTransaction(transaction.GetDbTransaction());
        return transaction;
    }
    public void RollbackTransaction() => Database.CurrentTransaction?.Rollback();
    public Task RollbackTransactionAsync() => Database.CurrentTransaction?.RollbackAsync() ?? Task.CompletedTask;
    public int GetPendingChangesCount() => ChangeTracker.Entries().Count(e => e.State == EntityState.Added || e.State == EntityState.Modified || e.State == EntityState.Deleted);
    public IReadOnlyList<Sirius.Domain.SeedWork.Entity> GetEntitiesWithPendingEvents() => new List<Sirius.Domain.SeedWork.Entity>();
}
