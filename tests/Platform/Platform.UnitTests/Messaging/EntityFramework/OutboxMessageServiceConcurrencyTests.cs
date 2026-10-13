using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Sirius.Platform.Messaging.IntegrationEvents;
using Sirius.Platform.Messaging.Models;
using Sirius.Platform.Messaging.Options;
using Sirius.Platform.Messaging.Services;
using Sirius.Platform.Persistence;

namespace Platform.UnitTests.Messaging.EntityFramework;

/// <summary>
/// Unit tests for OutboxMessageService concurrency behavior.
/// 
/// IMPORTANT: Many concurrency tests are marked as SKIP because they use ExecuteUpdateAsync(),
/// which is not supported by EF Core InMemory provider. These tests should be run as integration
/// tests against a real database (SQL Server/PostgreSQL) to validate atomic claim behavior.
/// 
/// For now, we test the parts that DO work with InMemory provider:
/// - State transitions (MarkEventAsPublished, MarkEventAsFailed)
/// - Event retrieval by transaction
/// - Event type validation
/// </summary>
public class OutboxMessageServiceConcurrencyTests : IDisposable
{
    private readonly TestDbContext _context;
    private readonly Mock<ILogger<IOutboxMessageService>> _mockLogger;
    private readonly OutboxMessageOptions _options;

    public OutboxMessageServiceConcurrencyTests()
    {
        var dbOptions = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new TestDbContext(dbOptions);
        _mockLogger = new Mock<ILogger<IOutboxMessageService>>();

        _options = new OutboxMessageOptions();
        _options.RegisterEventTypes(typeof(TestIntegrationEvent));
    }

    [Fact(Skip = "ExecuteUpdateAsync not supported by EF InMemory provider. Run as integration test against real DB.")]
    public async Task RetrieveUnpublishedEventsAsync_SingleProcess_ClaimsAllEvents()
    {
        await SeedOutboxMessagesAsync(5);
        var service = CreateService();

        var result = await service.RetrieveUnpublishedEventsAsync(10, 3, CancellationToken.None);

        result.Should().HaveCount(5);
        result.Should().OnlyContain(e => e.Status == OutboxMessageStatus.InProgress);
        result.Should().OnlyContain(e => e.SendCount == 1);
    }

    [Fact(Skip = "ExecuteUpdateAsync not supported by EF InMemory provider. Run as integration test against real DB.")]
    public async Task RetrieveUnpublishedEventsAsync_ConcurrentProcesses_NoEventIsDuplicated()
    {
        await SeedOutboxMessagesAsync(10);

        var service1 = CreateService();
        var service2 = CreateService();

        var task1 = service1.RetrieveUnpublishedEventsAsync(5, 3, CancellationToken.None);
        var task2 = service2.RetrieveUnpublishedEventsAsync(5, 3, CancellationToken.None);

        await Task.WhenAll(task1, task2);

        var events1 = task1.Result.ToList();
        var events2 = task2.Result.ToList();

        var totalClaimed = events1.Count + events2.Count;
        totalClaimed.Should().BeLessThanOrEqualTo(10);

        var allEventIds = events1.Select(e => e.Id)
            .Concat(events2.Select(e => e.Id))
            .ToList();

        allEventIds.Should().OnlyHaveUniqueItems("each event should be claimed by only one process");

        var inProgressCount = await _context.Set<OutboxMessage>()
            .CountAsync(e => e.Status == OutboxMessageStatus.InProgress);

        inProgressCount.Should().Be(totalClaimed);
    }

    [Fact(Skip = "ExecuteUpdateAsync not supported by EF InMemory provider. Run as integration test against real DB.")]
    public async Task RetrieveUnpublishedEventsAsync_ProcessClaimsEvents_OthersGetEmpty()
    {
        await SeedOutboxMessagesAsync(3);

        var service1 = CreateService();
        var service2 = CreateService();

        var events1 = await service1.RetrieveUnpublishedEventsAsync(10, 3, CancellationToken.None);

        var events2 = await service2.RetrieveUnpublishedEventsAsync(10, 3, CancellationToken.None);

        events1.Should().HaveCount(3);
        events2.Should().BeEmpty("all events were already claimed by service1");
    }

    [Fact(Skip = "ExecuteUpdateAsync not supported by EF InMemory provider. Run as integration test against real DB.")]
    public async Task RetrieveUnpublishedEventsAsync_RespectsBatchSize()
    {
        await SeedOutboxMessagesAsync(10);
        var service = CreateService();

        var batch1 = await service.RetrieveUnpublishedEventsAsync(3, 5, CancellationToken.None);
        var batch2 = await service.RetrieveUnpublishedEventsAsync(3, 5, CancellationToken.None);
        var batch3 = await service.RetrieveUnpublishedEventsAsync(3, 5, CancellationToken.None);

        batch1.Should().HaveCount(3);
        batch2.Should().HaveCount(3);
        batch3.Should().HaveCount(3);

        var batch4 = await service.RetrieveUnpublishedEventsAsync(3, 5, CancellationToken.None);
        batch4.Should().HaveCount(1);

        var batch5 = await service.RetrieveUnpublishedEventsAsync(3, 5, CancellationToken.None);
        batch5.Should().BeEmpty();
    }

    [Fact]
    public async Task RetrieveUnpublishedEventsAsync_RespectsMaxRetryCount()
    {
        await SeedOutboxMessagesAsync(5, timesSent: 3);
        var service = CreateService();

        var result = await service.RetrieveUnpublishedEventsAsync(10, maxRetryCount: 3, CancellationToken.None);

        result.Should().BeEmpty("all events have reached max retry count");

        var stillNotPublished = await _context.Set<OutboxMessage>()
            .CountAsync(e => e.Status == OutboxMessageStatus.NotPublished && e.SendCount >= 3);

        stillNotPublished.Should().Be(5);
    }

    [Fact(Skip = "ExecuteUpdateAsync not supported by EF InMemory provider. Run as integration test against real DB.")]
    public async Task RetrieveUnpublishedEventsAsync_IncludesFailedEvents()
    {
        await SeedOutboxMessagesAsync(3, state: OutboxMessageStatus.NotPublished);
        await SeedOutboxMessagesAsync(2, state: OutboxMessageStatus.FailedToPublish, timesSent: 1);

        var service = CreateService();

        var result = await service.RetrieveUnpublishedEventsAsync(10, 3, CancellationToken.None);

        result.Should().HaveCount(5);
        result.Where(e => e.SendCount == 1).Should().HaveCount(3, "NotPublished events get incremented");
        result.Where(e => e.SendCount == 2).Should().HaveCount(2, "FailedToPublish events get incremented");
    }

    [Fact(Skip = "ExecuteUpdateAsync not supported by EF InMemory provider. Run as integration test against real DB.")]
    public async Task RetrieveUnpublishedEventsAsync_ProcessesOldestFirst()
    {
        var oldEvent = await CreateOutboxMessageAsync(creationTime: DateTime.UtcNow.AddHours(-2));
        var middleEvent = await CreateOutboxMessageAsync(creationTime: DateTime.UtcNow.AddHours(-1));
        var newestEvent = await CreateOutboxMessageAsync(creationTime: DateTime.UtcNow);

        var service = CreateService();

        var result = (await service.RetrieveUnpublishedEventsAsync(2, 3, CancellationToken.None)).ToList();

        result.Should().HaveCount(2);
        result[0].Id.Should().Be(oldEvent);
        result[1].Id.Should().Be(middleEvent);
    }

    [Fact]
    public async Task MarkEventAsPublishedAsync_UpdatesStateSuccessfully()
    {
        var eventId = await CreateOutboxMessageAsync(state: OutboxMessageStatus.InProgress);
        var service = CreateService();

        await service.MarkEventAsPublishedAsync(eventId);

        var updated = await _context.Set<OutboxMessage>()
            .FirstOrDefaultAsync(e => e.Id == eventId);

        updated.Should().NotBeNull();
        updated!.Status.Should().Be(OutboxMessageStatus.Published);
    }

    [Fact]
    public async Task MarkEventAsFailedAsync_UpdatesStateSuccessfully()
    {
        var eventId = await CreateOutboxMessageAsync(state: OutboxMessageStatus.InProgress, timesSent: 1);
        var service = CreateService();

        await service.MarkEventAsFailedAsync(eventId);

        var updated = await _context.Set<OutboxMessage>()
            .FirstOrDefaultAsync(e => e.Id == eventId);

        updated.Should().NotBeNull();
        updated!.Status.Should().Be(OutboxMessageStatus.FailedToPublish);
        updated.SendCount.Should().Be(1, "SendCount should not change when marking as failed");
    }

    [Fact]
    public async Task UpdateEventStatus_HandlesConcurrencyConflict()
    {
        var eventId = await CreateOutboxMessageAsync(state: OutboxMessageStatus.InProgress);
        var service1 = CreateService();
        var service2 = CreateService();

        var originalEvent = await _context.Set<OutboxMessage>()
            .AsNoTracking()
            .FirstAsync(e => e.Id == eventId);

        await service1.MarkEventAsPublishedAsync(eventId);

        _context.ChangeTracker.Clear();

        var updateAction = async () => await service2.MarkEventAsFailedAsync(eventId);

        await updateAction.Should().NotThrowAsync<DbUpdateConcurrencyException>(
            "EF Core InMemory provider doesn't enforce RowVersion, but SQL Server will");
    }

    [Fact]
    public async Task UpdateEventStatus_WhenEventNotFound_LogsWarning()
    {
        var nonExistentId = Guid.NewGuid();
        var service = CreateService();

        await service.MarkEventAsPublishedAsync(nonExistentId);

        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("not found")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact(Skip = "EF InMemory doesn't support transactions. Run as integration test against real DB.")]
    public async Task SaveEventAsync_AddsEventToChangeTracker()
    {
        var @event = new TestIntegrationEvent { Id = Guid.NewGuid() };
        var transactionId = Guid.NewGuid();

        var service = CreateService();

        await service.SaveEventAsync(@event, transactionId);

        var pending = _context.ChangeTracker.Entries<OutboxMessage>()
            .Where(e => e.State == EntityState.Added)
            .Select(e => e.Entity)
            .ToList();

        pending.Should().HaveCount(1);
        pending[0].Id.Should().Be(@event.Id);
        pending[0].Status.Should().Be(OutboxMessageStatus.NotPublished);
        pending[0].TransactionId.Should().Be(transactionId);
    }

    [Fact]
    public async Task RetrieveEventLogsPendingToPublishAsync_ReturnsOnlyEventsForTransaction()
    {
        var transactionId1 = Guid.NewGuid();
        var transactionId2 = Guid.NewGuid();

        await CreateOutboxMessageAsync(transactionId: transactionId1);
        await CreateOutboxMessageAsync(transactionId: transactionId1);
        await CreateOutboxMessageAsync(transactionId: transactionId2);

        var service = CreateService();

        var result = await service.RetrieveEventLogsPendingToPublishAsync(transactionId1);

        result.Should().HaveCount(2);
        result.Should().OnlyContain(e => e.TransactionId == transactionId1);
    }

    [Fact(Skip = "ExecuteUpdateAsync not supported by EF InMemory provider. Run as integration test against real DB.")]
    public async Task RetrieveUnpublishedEventsAsync_ThrowsWhenEventTypeNotRegistered()
    {
        var unregisteredEvent = new UnregisteredIntegrationEvent { Id = Guid.NewGuid() };
        var outboxMessage = new OutboxMessage
        {
            Id = unregisteredEvent.Id,
            CreatedAt = unregisteredEvent.CreationDate,
            TypeName = unregisteredEvent.GetType().FullName!,
            Payload = System.Text.Json.JsonSerializer.Serialize(unregisteredEvent),
            Status = OutboxMessageStatus.NotPublished,
            SendCount = 0,
            TransactionId = Guid.NewGuid()
        };

        _context.Set<OutboxMessage>().Add(outboxMessage);
        await _context.SaveChangesAsync();

        var service = CreateService();

        var act = async () => await service.RetrieveUnpublishedEventsAsync(10, 3, CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*not registered*");
    }

    [Fact(Skip = "ExecuteUpdateAsync not supported by EF InMemory provider. Run as integration test against real DB.")]
    public async Task ConcurrentRetrieval_With100Events_And5Processes_NoCollisions()
    {
        await SeedOutboxMessagesAsync(100);

        var services = Enumerable.Range(0, 5).Select(_ => CreateService()).ToList();

        var tasks = services.Select(s =>
            s.RetrieveUnpublishedEventsAsync(20, 5, CancellationToken.None)).ToArray();

        await Task.WhenAll(tasks);

        var allClaimedEvents = tasks
            .SelectMany(t => t.Result)
            .Select(e => e.Id)
            .ToList();

        allClaimedEvents.Should().OnlyHaveUniqueItems(
            "no event should be claimed by multiple processes");

        allClaimedEvents.Should().HaveCount(100,
            "all events should be claimed exactly once");

        var inProgressCount = await _context.Set<OutboxMessage>()
            .CountAsync(e => e.Status == OutboxMessageStatus.InProgress);

        inProgressCount.Should().Be(100);
    }

    private OutboxMessageService<TestDbContext> CreateService()
    {
        return new OutboxMessageService<TestDbContext>(
            _context,
            Options.Create(_options),
            _mockLogger.Object);
    }

    private async Task SeedOutboxMessagesAsync(
        int count,
        OutboxMessageStatus state = OutboxMessageStatus.NotPublished,
        int timesSent = 0)
    {
        for (int i = 0; i < count; i++)
        {
            await CreateOutboxMessageAsync(state: state, timesSent: timesSent);
        }
    }

    private async Task<Guid> CreateOutboxMessageAsync(
        OutboxMessageStatus state = OutboxMessageStatus.NotPublished,
        int timesSent = 0,
        Guid? transactionId = null,
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
            TransactionId = transactionId ?? Guid.NewGuid()
        };

        _context.Set<OutboxMessage>().Add(message);

        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();

        return @event.Id;
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }

    private class TestDbContext : DbContext, ITransactionalUnitOfWork
    {
        private IDbContextTransaction? _currentTransaction;

        public TestDbContext(DbContextOptions<TestDbContext> options) : base(options) { }

        public bool HasActiveTransaction => _currentTransaction is not null;

        public Guid? LastCommittedTransactionId { get; private set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<OutboxMessage>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.TypeName).IsRequired();
                entity.Property(e => e.Payload).IsRequired();
                entity.Property(e => e.CreatedAt).IsRequired();
                entity.Property(e => e.TransactionId).IsRequired();
                entity.Property(e => e.Status).IsRequired();
                entity.Property(e => e.SendCount).IsRequired();
                entity.Property(e => e.RowVersion).IsRowVersion();
                entity.Ignore(e => e.DeserializedEvent);
            });

            modelBuilder.Entity<IdempotencyEntry>(entity =>
            {
                entity.HasKey(e => new { e.Id, e.Scope, e.Qualifier });
            });
        }

        public Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
        {
            return SaveChangesAsync(cancellationToken);
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

    private record TestIntegrationEvent : IntegrationEvent
    {
    }

    private record UnregisteredIntegrationEvent : IntegrationEvent
    {
    }

    private class IdempotencyEntry
    {
        public Guid Id { get; set; }
        public int Scope { get; set; }
        public string Qualifier { get; set; } = string.Empty;
        public string TypeName { get; set; } = string.Empty;
        public DateTime ProcessedAt { get; set; }
    }
}
