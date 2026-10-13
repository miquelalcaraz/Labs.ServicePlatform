using System;
using System.Threading.Tasks;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Sirius.Platform.Messaging.Models;
using Sirius.Platform.Messaging.Options;

using StackExchange.Redis;

namespace Sirius.Platform.Messaging.Services;

/// <summary>
/// Redis-based idempotency service with resilient fallback on failures.
/// </summary>
public sealed class IdempotencyRedisService : IIdempotencyService
{
    private readonly IConnectionMultiplexer _redis;
    private readonly ILogger<IdempotencyRedisService> _logger;
    private readonly IdempotencyCacheOptions _options;

    public IdempotencyRedisService(
        IConnectionMultiplexer redis,
        IOptions<IdempotencyCacheOptions> options,
        ILogger<IdempotencyRedisService> logger)
    {
        _redis = redis ?? throw new ArgumentNullException(nameof(redis));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
    }

    public async Task<bool> ExistsAsync(IdempotencyKey key)
    {
        try
        {
            var db = _redis.GetDatabase();
            var redisKey = GetRedisKey(key);
            return await db.KeyExistsAsync(redisKey);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Failed to check Redis cache for {Scope} key {Id}",
                key.Scope, key.Id);
            return false;
        }
    }

    public async Task MarkAsProcessingAsync(IdempotencyKey key, string? typeName = null)
    {
        try
        {
            var db = _redis.GetDatabase();
            var redisKey = GetRedisKey(key);
            await db.StringSetAsync(redisKey, "processing", _options.ProcessingTtl);

            _logger.LogDebug("Marked {Scope} key {Id} as processing in cache with TTL {Ttl}",
                key.Scope, key.Id, _options.ProcessingTtl);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Failed to mark {Scope} key {Id} as processing in Redis cache",
                key.Scope, key.Id);
        }
    }

    public async Task MarkAsCompletedAsync(IdempotencyKey key)
    {
        try
        {
            var db = _redis.GetDatabase();
            var redisKey = GetRedisKey(key);
            await db.StringSetAsync(redisKey, "completed", _options.CompletedTtl);

            _logger.LogDebug("Marked {Scope} key {Id} as completed in cache with TTL {Ttl}",
                key.Scope, key.Id, _options.CompletedTtl);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Failed to mark {Scope} key {Id} as completed in Redis cache",
                key.Scope, key.Id);
        }
    }

    public async Task DiscardAsync(IdempotencyKey key)
    {
        try
        {
            var db = _redis.GetDatabase();
            var redisKey = GetRedisKey(key);
            await db.KeyDeleteAsync(redisKey);

            _logger.LogDebug("Discarded {Scope} key {Id} from cache after failure",
                key.Scope, key.Id);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Failed to discard {Scope} key {Id} from Redis cache",
                key.Scope, key.Id);
        }
    }

    private string GetRedisKey(IdempotencyKey key) =>
        string.IsNullOrEmpty(key.Qualifier)
            ? $"{_options.KeyPrefix}{key.Scope.ToString().ToLowerInvariant()}:{key.Id}"
            : $"{_options.KeyPrefix}{key.Scope.ToString().ToLowerInvariant()}:{key.Id}:{key.Qualifier}";
}
