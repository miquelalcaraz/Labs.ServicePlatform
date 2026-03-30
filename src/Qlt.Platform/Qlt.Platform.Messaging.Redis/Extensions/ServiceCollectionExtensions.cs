using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using Qlt.Platform.Messaging.Builders;
using Qlt.Platform.Messaging.Configuration;
using Qlt.Platform.Messaging.Idempotency;

using StackExchange.Redis;

using System;

namespace Qlt.Platform.Messaging.Extensions;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers Redis-based idempotency service and returns a fluent builder for further configuration.
    /// </summary>
    public static RedisIdempotencyBuilder AddRedisIdempotencyCache(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<IdempotencyCacheOptions>()
            .Bind(configuration.GetSection(IdempotencyCacheOptions.SectionName));

        services.AddScoped<IIdempotencyService, IdempotencyRedisService>();

        return new RedisIdempotencyBuilder(services);
    }

    public static RedisIdempotencyBuilder AddRedisIdempotencyCache(this IServiceCollection services, IConfiguration configuration, string redisConnectionString)
    {
        services.AddSingleton<IConnectionMultiplexer>(sp =>
            ConnectionMultiplexer.Connect(redisConnectionString));

        return services.AddRedisIdempotencyCache(configuration);
    }

    public static RedisIdempotencyBuilder AddRedisIdempotencyCache(this IServiceCollection services, Action<IdempotencyCacheOptions> configureOptions)
    {
        services.Configure(configureOptions);
        services.AddScoped<IIdempotencyService, IdempotencyRedisService>();

        return new RedisIdempotencyBuilder(services);
    }
}
