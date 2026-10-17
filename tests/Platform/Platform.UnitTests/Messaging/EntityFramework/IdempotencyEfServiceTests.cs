using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;

using Sirius.Platform.Messaging.Models;
using Sirius.Platform.Messaging.Services;
using Sirius.Platform.Persistence;

namespace Platform.UnitTests.Messaging.EntityFramework;

public class IdempotencyEfServiceTests : IDisposable
{
    private readonly TestDbContext _context;
    private readonly Mock<IIdempotencyService> _mockInner;
    private readonly Mock<ILogger<IdempotencyEfService<TestDbContext>>> _mockLogger;

    public IdempotencyEfServiceTests()
    {
        var options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new TestDbContext(options);
        _mockInner = new Mock<IIdempotencyService>();
        _mockLogger = new Mock<ILogger<IdempotencyEfService<TestDbContext>>>();
    }

    [Fact]
    public async Task ExistsAsync_WhenExistsInCache_ReturnsTrue()
    {
        var key = IdempotencyKey.ForRequest(Guid.NewGuid());
        _mockInner.Setup(c => c.ExistsAsync(key))
            .ReturnsAsync(true);

        var service = CreateService();

        var result = await service.ExistsAsync(key);

        result.Should().BeTrue();
        _mockInner.Verify(c => c.ExistsAsync(key), Times.Once);
    }

    [Fact]
    public async Task ExistsAsync_WhenNotInCacheButInDb_ReturnsTrue()
    {
        var key = IdempotencyKey.ForRequest(Guid.NewGuid());
        await _context.Set<IdempotencyEntry>().AddAsync(new IdempotencyEntry
        {
            Id = key.Id,
            Scope = IdempotencyScope.Request,
            Qualifier = string.Empty,
            TypeName = "TestCommand",
            ProcessedAt = DateTime.UtcNow
        });
        await _context.SaveChangesAsync();

        _mockInner.Setup(c => c.ExistsAsync(key))
            .ReturnsAsync(false);

        var service = CreateService();

        var result = await service.ExistsAsync(key);

        result.Should().BeTrue();
    }

    [Fact]
    public async Task ExistsAsync_WhenNotExists_ReturnsFalse()
    {
        var key = IdempotencyKey.ForRequest(Guid.NewGuid());
        _mockInner.Setup(c => c.ExistsAsync(key))
            .ReturnsAsync(false);

        var service = CreateService();

        var result = await service.ExistsAsync(key);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task ExistsAsync_EventScope_QueriesWithQualifier()
    {
        var eventId = Guid.NewGuid();
        var key = IdempotencyKey.ForEvent(eventId, "MyHandler");

        await _context.Set<IdempotencyEntry>().AddAsync(new IdempotencyEntry
        {
            Id = eventId,
            Scope = IdempotencyScope.Event,
            Qualifier = "MyHandler",
            TypeName = "TestEvent",
            ProcessedAt = DateTime.UtcNow
        });
        await _context.SaveChangesAsync();

        _mockInner.Setup(c => c.ExistsAsync(key))
            .ReturnsAsync(false);

        var service = CreateService();

        var result = await service.ExistsAsync(key);

        result.Should().BeTrue();
    }

    [Fact]
    public async Task MarkAsProcessingAsync_DelegatesToInnerAndAddsToDb()
    {
        var key = IdempotencyKey.ForRequest(Guid.NewGuid());
        var service = CreateService();

        await service.MarkAsProcessingAsync(key, "TestCommand");
        await _context.SaveChangesAsync();

        var entry = await _context.Set<IdempotencyEntry>()
            .FirstOrDefaultAsync(e => e.Id == key.Id && e.Scope == IdempotencyScope.Request);

        entry.Should().NotBeNull();
        entry!.TypeName.Should().Be("TestCommand");

        _mockInner.Verify(c => c.MarkAsProcessingAsync(key, "TestCommand"), Times.Once);
    }

    [Fact]
    public async Task MarkAsProcessingAsync_SetsCorrectTimestamp()
    {
        var key = IdempotencyKey.ForRequest(Guid.NewGuid());
        var before = DateTime.UtcNow;

        var service = CreateService();
        await service.MarkAsProcessingAsync(key, "TestCommand");
        await _context.SaveChangesAsync();

        var after = DateTime.UtcNow;
        var entry = await _context.Set<IdempotencyEntry>()
            .FirstOrDefaultAsync(e => e.Id == key.Id);

        entry.Should().NotBeNull();
        entry!.ProcessedAt.Should().BeOnOrAfter(before).And.BeOnOrBefore(after);
    }

    [Fact]
    public async Task ExistsAsync_LogsDebugWhenFoundInCache()
    {
        var key = IdempotencyKey.ForRequest(Guid.NewGuid());
        _mockInner.Setup(c => c.ExistsAsync(key))
            .ReturnsAsync(true);

        var service = CreateService();

        await service.ExistsAsync(key);

        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Debug,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("found in cache")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task Standalone_ExistsAsync_WhenInDb_ReturnsTrue()
    {
        var key = IdempotencyKey.ForRequest(Guid.NewGuid());
        await _context.Set<IdempotencyEntry>().AddAsync(new IdempotencyEntry
        {
            Id = key.Id,
            Scope = IdempotencyScope.Request,
            Qualifier = string.Empty,
            TypeName = "TestCommand",
            ProcessedAt = DateTime.UtcNow
        });
        await _context.SaveChangesAsync();

        var service = CreateStandaloneService();

        var result = await service.ExistsAsync(key);

        result.Should().BeTrue();
    }

    [Fact]
    public async Task Standalone_ExistsAsync_WhenNotInDb_ReturnsFalse()
    {
        var key = IdempotencyKey.ForRequest(Guid.NewGuid());
        var service = CreateStandaloneService();

        var result = await service.ExistsAsync(key);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task Standalone_MarkAsProcessingAsync_AddsEntryToDb()
    {
        var key = IdempotencyKey.ForRequest(Guid.NewGuid());
        var service = CreateStandaloneService();

        await service.MarkAsProcessingAsync(key, "TestCommand");
        await _context.SaveChangesAsync();

        var entry = await _context.Set<IdempotencyEntry>()
            .FirstOrDefaultAsync(e => e.Id == key.Id && e.Scope == IdempotencyScope.Request);

        entry.Should().NotBeNull();
        entry!.TypeName.Should().Be("TestCommand");
    }

    [Fact]
    public async Task Standalone_MarkAsCompletedAsync_DoesNotThrow()
    {
        var key = IdempotencyKey.ForRequest(Guid.NewGuid());
        var service = CreateStandaloneService();

        var act = () => service.MarkAsCompletedAsync(key);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task MarkAsCompletedAsync_DelegatesToInner()
    {
        var key = IdempotencyKey.ForRequest(Guid.NewGuid());
        var service = CreateService();

        await service.MarkAsCompletedAsync(key);

        _mockInner.Verify(c => c.MarkAsCompletedAsync(key), Times.Once);
    }

    [Fact]
    public async Task DiscardAsync_DelegatesToInnerAndDetachesTrackedEntry()
    {
        var key = IdempotencyKey.ForRequest(Guid.NewGuid());
        var service = CreateService();

        await service.MarkAsProcessingAsync(key, "TestCommand");

        _context.ChangeTracker.Entries<IdempotencyEntry>()
            .Should().ContainSingle(e => e.Entity.Id == key.Id);

        await service.DiscardAsync(key);

        _mockInner.Verify(c => c.DiscardAsync(key), Times.Once);
        _context.ChangeTracker.Entries<IdempotencyEntry>()
            .Should().BeEmpty();
    }

    [Fact]
    public async Task DiscardAsync_WhenNoTrackedEntry_DoesNotThrow()
    {
        var key = IdempotencyKey.ForRequest(Guid.NewGuid());
        var service = CreateService();

        var act = () => service.DiscardAsync(key);

        await act.Should().NotThrowAsync();
        _mockInner.Verify(c => c.DiscardAsync(key), Times.Once);
    }

    [Fact]
    public async Task DiscardAsync_OnlyDetachesMatchingEntry()
    {
        var discardedKey = IdempotencyKey.ForRequest(Guid.NewGuid());
        var keptKey = IdempotencyKey.ForEvent(Guid.NewGuid(), "MyHandler");
        var service = CreateService();

        await service.MarkAsProcessingAsync(discardedKey, "TestCommand");
        await service.MarkAsProcessingAsync(keptKey, "TestEvent");

        await service.DiscardAsync(discardedKey);

        _context.ChangeTracker.Entries<IdempotencyEntry>()
            .Should().ContainSingle(e => e.Entity.Id == keptKey.Id);
    }

    [Fact]
    public async Task Standalone_DiscardAsync_EntryIsNotPersistedOnSave()
    {
        var key = IdempotencyKey.ForRequest(Guid.NewGuid());
        var service = CreateStandaloneService();

        await service.MarkAsProcessingAsync(key, "TestCommand");
        await service.DiscardAsync(key);
        await _context.SaveChangesAsync();

        var exists = await _context.Set<IdempotencyEntry>().AnyAsync(e => e.Id == key.Id);

        exists.Should().BeFalse();
    }

    private IdempotencyEfService<TestDbContext> CreateService()
    {
        return new IdempotencyEfService<TestDbContext>(
            _context,
            _mockInner.Object,
            _mockLogger.Object);
    }

    private IdempotencyEfService<TestDbContext> CreateStandaloneService()
    {
        return new IdempotencyEfService<TestDbContext>(
            _context,
            _mockLogger.Object);
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }

    public class TestDbContext : DbContext, ITransactionalUnitOfWork
    {
        public TestDbContext(DbContextOptions<TestDbContext> options) : base(options) { }

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
            await transaction.CommitAsync();
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
}
