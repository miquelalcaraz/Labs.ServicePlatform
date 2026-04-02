using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Qlt.Platform.Messaging.Models;
using Qlt.Platform.Messaging.Options;
using Qlt.Platform.Messaging.Services;

using StackExchange.Redis;

namespace Platform.UnitTests.Messaging.Redis;

public class IdempotencyRedisServiceTests
{
    private readonly Mock<IConnectionMultiplexer> _mockRedis;
    private readonly Mock<IDatabase> _mockDatabase;
    private readonly Mock<ILogger<IdempotencyRedisService>> _mockLogger;
    private readonly IdempotencyCacheOptions _options;

    public IdempotencyRedisServiceTests()
    {
        _mockRedis = new Mock<IConnectionMultiplexer>();
        _mockDatabase = new Mock<IDatabase>();
        _mockLogger = new Mock<ILogger<IdempotencyRedisService>>();

        _options = new IdempotencyCacheOptions
        {
            ProcessingTtl = TimeSpan.FromSeconds(30),
            CompletedTtl = TimeSpan.FromMinutes(5),
            KeyPrefix = "idempotency:"
        };

        _mockRedis.Setup(r => r.GetDatabase(It.IsAny<int>(), It.IsAny<object>()))
            .Returns(_mockDatabase.Object);
    }

    [Fact]
    public async Task ExistsAsync_WhenKeyExists_ReturnsTrue()
    {
        var key = IdempotencyKey.ForRequest(Guid.NewGuid());
        _mockDatabase.Setup(db => db.KeyExistsAsync(
            It.Is<RedisKey>(k => k == $"idempotency:request:{key.Id}"),
            It.IsAny<CommandFlags>()))
            .ReturnsAsync(true);

        var service = CreateService();

        var result = await service.ExistsAsync(key);

        result.Should().BeTrue();
    }

    [Fact]
    public async Task ExistsAsync_WhenKeyDoesNotExist_ReturnsFalse()
    {
        var key = IdempotencyKey.ForRequest(Guid.NewGuid());
        _mockDatabase.Setup(db => db.KeyExistsAsync(
            It.IsAny<RedisKey>(),
            It.IsAny<CommandFlags>()))
            .ReturnsAsync(false);

        var service = CreateService();

        var result = await service.ExistsAsync(key);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task ExistsAsync_WhenRedisThrowsException_ReturnsFalseAndLogs()
    {
        var key = IdempotencyKey.ForRequest(Guid.NewGuid());
        _mockDatabase.Setup(db => db.KeyExistsAsync(
            It.IsAny<RedisKey>(),
            It.IsAny<CommandFlags>()))
            .ThrowsAsync(new RedisConnectionException(ConnectionFailureType.UnableToConnect, "Connection failed"));

        var service = CreateService();

        var result = await service.ExistsAsync(key);

        result.Should().BeFalse();
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Failed to check Redis cache")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task ExistsAsync_EventKey_UsesCorrectRedisKeyFormat()
    {
        var eventId = Guid.NewGuid();
        var key = IdempotencyKey.ForEvent(eventId, "MyHandler");
        _mockDatabase.Setup(db => db.KeyExistsAsync(
            It.Is<RedisKey>(k => k == $"idempotency:event:{eventId}:MyHandler"),
            It.IsAny<CommandFlags>()))
            .ReturnsAsync(true);

        var service = CreateService();

        var result = await service.ExistsAsync(key);

        result.Should().BeTrue();
    }

    [Fact]
    public async Task MarkAsProcessingAsync_WhenRedisFails_DoesNotThrow()
    {
        var key = IdempotencyKey.ForRequest(Guid.NewGuid());
        _mockDatabase.Setup(db => db.StringSetAsync(
            It.IsAny<RedisKey>(),
            It.IsAny<RedisValue>(),
            It.IsAny<Expiration>(),
            It.IsAny<ValueCondition>(),
            It.IsAny<CommandFlags>()))
            .ThrowsAsync(new TimeoutException("Redis timeout"));

        var service = CreateService();

        var act = async () => await service.MarkAsProcessingAsync(key);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task MarkAsProcessingAsync_SetsKeyInRedis()
    {
        var key = IdempotencyKey.ForRequest(Guid.NewGuid());
        var service = CreateService();

        await service.MarkAsProcessingAsync(key, "TestCommand");

        _mockDatabase.Verify(db => db.StringSetAsync(
            It.Is<RedisKey>(k => k == $"idempotency:request:{key.Id}"),
            It.Is<RedisValue>(v => v == "processing"),
            It.IsAny<Expiration>(),
            It.IsAny<ValueCondition>(),
            It.IsAny<CommandFlags>()),
            Times.Once);
    }

    [Fact]
    public async Task MarkAsCompletedAsync_SetsKeyInRedis()
    {
        var key = IdempotencyKey.ForRequest(Guid.NewGuid());
        var service = CreateService();

        await service.MarkAsCompletedAsync(key);

        _mockDatabase.Verify(db => db.StringSetAsync(
            It.Is<RedisKey>(k => k == $"idempotency:request:{key.Id}"),
            It.Is<RedisValue>(v => v == "completed"),
            It.IsAny<Expiration>(),
            It.IsAny<ValueCondition>(),
            It.IsAny<CommandFlags>()),
            Times.Once);
    }

    [Fact]
    public async Task MarkAsCompletedAsync_WhenRedisFails_DoesNotThrow()
    {
        var key = IdempotencyKey.ForRequest(Guid.NewGuid());
        _mockDatabase.Setup(db => db.StringSetAsync(
            It.IsAny<RedisKey>(),
            It.IsAny<RedisValue>(),
            It.IsAny<Expiration>(),
            It.IsAny<ValueCondition>(),
            It.IsAny<CommandFlags>()))
            .ThrowsAsync(new TimeoutException("Redis timeout"));

        var service = CreateService();

        var act = async () => await service.MarkAsCompletedAsync(key);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public void Constructor_WithNullRedis_ThrowsArgumentNullException()
    {
        var act = () => new IdempotencyRedisService(
            null!,
            Options.Create(_options),
            _mockLogger.Object);

        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("redis");
    }

    [Fact]
    public void Constructor_WithNullOptions_ThrowsArgumentNullException()
    {
        var act = () => new IdempotencyRedisService(
            _mockRedis.Object,
            null!,
            _mockLogger.Object);

        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("options");
    }

    [Fact]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        var act = () => new IdempotencyRedisService(
            _mockRedis.Object,
            Options.Create(_options),
            null!);

        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("logger");
    }

    private IdempotencyRedisService CreateService()
    {
        return new IdempotencyRedisService(
            _mockRedis.Object,
            Options.Create(_options),
            _mockLogger.Object);
    }
}
