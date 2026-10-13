using Platform.IntegrationTests.Infrastructure;

using Miqa.Platform.Messaging.Models;
using Miqa.Platform.Messaging.Options;
using Miqa.Platform.Messaging.Services;

namespace Platform.IntegrationTests.Messaging;

public class OutboxConcurrencyIntegrationTests : IntegrationTestBase
{
    [Fact]
    public async Task RetrieveUnpublishedEventsAsync_SingleProcess_ClaimsAllEvents()
    {
        for (int i = 0; i < 5; i++)
        {
            await SeedOutboxMessageAsync();
        }

        var service = CreateOutboxService();

        var result = await service.RetrieveUnpublishedEventsAsync(10, 3, CancellationToken.None);

        result.Should().HaveCount(5);
        result.Should().OnlyContain(e => e.Status == OutboxMessageStatus.InProgress);
        result.Should().OnlyContain(e => e.SendCount == 1);
    }

    [Fact]
    public async Task RetrieveUnpublishedEventsAsync_TwoProcessesConcurrently_NoEventDuplication()
    {
        for (int i = 0; i < 10; i++)
        {
            await SeedOutboxMessageAsync();
        }

        var service1 = CreateIsolatedOutboxService();
        var service2 = CreateIsolatedOutboxService();

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

        var inProgressCount = await DbContext.Set<OutboxMessage>()
            .CountAsync(e => e.Status == OutboxMessageStatus.InProgress);

        inProgressCount.Should().Be(totalClaimed);
    }

    [Fact]
    public async Task RetrieveUnpublishedEventsAsync_FiveProcessesConcurrently_With100Events_NoCollisions()
    {
        for (int i = 0; i < 100; i++)
        {
            await SeedOutboxMessageAsync();
        }

        var tasks = Enumerable.Range(0, 5).Select(async processId =>
        {
            var dbOptions = new DbContextOptionsBuilder<IntegrationTestBase.TestDbContext>()
                .UseSqlServer(DbContext.Database.GetConnectionString())
                .Options;
            await using var isolatedContext = new IntegrationTestBase.TestDbContext(dbOptions);

            var options = new OutboxMessageOptions();
            options.RegisterEventTypes(typeof(TestIntegrationEvent));
            var service = new OutboxMessageService<IntegrationTestBase.TestDbContext>(
                isolatedContext,
                Options.Create(options),
                LoggerFactory.Create(builder => builder.AddConsole())
                    .CreateLogger<IOutboxMessageService>());

            var claimed = new List<Guid>();
            while (true)
            {
                var batch = await service.RetrieveUnpublishedEventsAsync(20, 5, CancellationToken.None);
                if (!batch.Any()) break;
                claimed.AddRange(batch.Select(e => e.Id));
            }
            return claimed;
        }).ToArray();

        var results = await Task.WhenAll(tasks);

        var allClaimedEvents = results.SelectMany(guids => guids).ToList();

        allClaimedEvents.Should().OnlyHaveUniqueItems(
            "no event should be claimed by multiple processes");

        allClaimedEvents.Should().HaveCount(100,
            "all events should be claimed exactly once");

        var inProgressCount = await DbContext.Set<OutboxMessage>()
            .CountAsync(e => e.Status == OutboxMessageStatus.InProgress);

        inProgressCount.Should().Be(100);

        var allEventsIncrementedOnce = await DbContext.Set<OutboxMessage>()
            .AllAsync(e => e.SendCount == 1);

        allEventsIncrementedOnce.Should().BeTrue("all events should have SendCount=1 after being claimed");
    }

    [Fact]
    public async Task RetrieveUnpublishedEventsAsync_ProcessClaimsEvents_SecondProcessGetsEmpty()
    {
        for (int i = 0; i < 3; i++)
        {
            await SeedOutboxMessageAsync();
        }

        var service1 = CreateOutboxService();
        var service2 = CreateOutboxService();

        var events1 = await service1.RetrieveUnpublishedEventsAsync(10, 3, CancellationToken.None);

        await Task.Delay(100);

        var events2 = await service2.RetrieveUnpublishedEventsAsync(10, 3, CancellationToken.None);

        events1.Should().HaveCount(3);
        events2.Should().BeEmpty("all events were already claimed by service1");
    }

    [Fact]
    public async Task RetrieveUnpublishedEventsAsync_RespectsBatchSize()
    {
        for (int i = 0; i < 10; i++)
        {
            await SeedOutboxMessageAsync();
        }

        var service = CreateOutboxService();

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
        for (int i = 0; i < 5; i++)
        {
            await SeedOutboxMessageAsync(timesSent: 3);
        }

        var service = CreateOutboxService();

        var result = await service.RetrieveUnpublishedEventsAsync(10, maxRetryCount: 3, CancellationToken.None);

        result.Should().BeEmpty("all events have reached max retry count");

        var stillNotPublished = await DbContext.Set<OutboxMessage>()
            .CountAsync(e => e.Status == OutboxMessageStatus.NotPublished && e.SendCount >= 3);

        stillNotPublished.Should().Be(5);
    }

    [Fact]
    public async Task RetrieveUnpublishedEventsAsync_IncludesFailedEvents()
    {
        for (int i = 0; i < 3; i++)
        {
            await SeedOutboxMessageAsync(state: OutboxMessageStatus.NotPublished);
        }
        for (int i = 0; i < 2; i++)
        {
            await SeedOutboxMessageAsync(state: OutboxMessageStatus.FailedToPublish, timesSent: 1);
        }

        var service = CreateOutboxService();

        var result = await service.RetrieveUnpublishedEventsAsync(10, 3, CancellationToken.None);

        result.Should().HaveCount(5);
        result.Where(e => e.SendCount == 1).Should().HaveCount(3, "NotPublished events get incremented");
        result.Where(e => e.SendCount == 2).Should().HaveCount(2, "FailedToPublish events get incremented");
    }

    [Fact]
    public async Task RetrieveUnpublishedEventsAsync_ProcessesOldestFirst()
    {
        var oldEvent = await SeedOutboxMessageAsync(creationTime: DateTime.UtcNow.AddHours(-2));
        var middleEvent = await SeedOutboxMessageAsync(creationTime: DateTime.UtcNow.AddHours(-1));
        var newestEvent = await SeedOutboxMessageAsync(creationTime: DateTime.UtcNow);

        var service = CreateOutboxService();

        var result = (await service.RetrieveUnpublishedEventsAsync(2, 3, CancellationToken.None)).ToList();

        result.Should().HaveCount(2);
        result[0].Id.Should().Be(oldEvent);
        result[1].Id.Should().Be(middleEvent);
    }

    [Fact(Skip = "RowVersion concurrency detection needs further investigation with SQL Server isolation levels")]
    public async Task UpdateEventStatus_WithRowVersion_DetectsConcurrencyConflict()
    {
        var eventId = await SeedOutboxMessageAsync(state: OutboxMessageStatus.InProgress);

        var service1 = CreateOutboxService();

        // Create a second service with a NEW DbContext instance
        var options2 = new DbContextOptionsBuilder<IntegrationTestBase.TestDbContext>()
            .UseSqlServer(DbContext.Database.GetConnectionString())
            .Options;
        var dbContext2 = new IntegrationTestBase.TestDbContext(options2);

        var optionsConfig = new OutboxMessageOptions();
        optionsConfig.RegisterEventTypes(typeof(TestIntegrationEvent));
        var service2 = new OutboxMessageService<IntegrationTestBase.TestDbContext>(
            dbContext2,
            Options.Create(optionsConfig),
            LoggerFactory.Create(builder => builder.AddConsole())
                .CreateLogger<IOutboxMessageService>());

        // Process 1 updates the event
        await service1.MarkEventAsPublishedAsync(eventId);

        // Process 2 tries to update the same event (should detect concurrency via RowVersion)
        var act = async () => await service2.MarkEventAsFailedAsync(eventId);

        await act.Should().ThrowAsync<DbUpdateConcurrencyException>(
            "the RowVersion should detect concurrent modifications");

        await dbContext2.DisposeAsync();
    }

    [Fact]
    public async Task StressTest_10ProcessesClaiming200Events_AllClaimedOnce()
    {
        const int eventCount = 200;
        const int processCount = 10;

        for (int i = 0; i < eventCount; i++)
        {
            await SeedOutboxMessageAsync();
        }

        // Create separate DbContext and service for each process
        var tasks = Enumerable.Range(0, processCount).Select(async processId =>
        {
            // Each process gets its own DbContext
            var options = new DbContextOptionsBuilder<IntegrationTestBase.TestDbContext>()
                .UseSqlServer(DbContext.Database.GetConnectionString())
                .Options;
            await using var dbContext = new IntegrationTestBase.TestDbContext(options);

            var optionsConfig = new OutboxMessageOptions();
            optionsConfig.RegisterEventTypes(typeof(TestIntegrationEvent));
            var service = new OutboxMessageService<IntegrationTestBase.TestDbContext>(
                dbContext,
                Options.Create(optionsConfig),
                LoggerFactory.Create(builder => builder.AddConsole())
                    .CreateLogger<IOutboxMessageService>());

            var claimed = new List<Guid>();
            while (true)
            {
                var batch = await service.RetrieveUnpublishedEventsAsync(10, 5, CancellationToken.None);
                if (!batch.Any()) break;
                claimed.AddRange(batch.Select(e => e.Id));
                await Task.Delay(10);
            }
            return claimed;
        }).ToArray();

        var results = await Task.WhenAll(tasks);

        var allClaimed = results.SelectMany(r => r).ToList();

        allClaimed.Should().HaveCount(eventCount, "all events should be claimed");
        allClaimed.Should().OnlyHaveUniqueItems("no event should be claimed twice");

        var allEventsProcessed = await DbContext.Set<OutboxMessage>()
            .AllAsync(e => e.Status == OutboxMessageStatus.InProgress && e.SendCount == 1);

        allEventsProcessed.Should().BeTrue("all events should be in InProgress state with SendCount=1");
    }
}
