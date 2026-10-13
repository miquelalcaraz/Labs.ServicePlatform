using System;
using System.Linq;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Sirius.Platform.Messaging.IntegrationEvents;
using Sirius.Platform.Messaging.Services;

namespace Sirius.Platform.Messaging.Builders;

/// <summary>
/// Fluent builder for configuring Redis-based idempotency.
/// </summary>
public class RedisIdempotencyBuilder
{
    private readonly IServiceCollection _services;

    internal RedisIdempotencyBuilder(IServiceCollection services)
    {
        _services = services;
    }

    /// <summary>
    /// Enables idempotency for API command requests.
    /// Redis is already registered — no additional configuration needed.
    /// </summary>
    public RedisIdempotencyBuilder ForApiRequests()
    {
        return this;
    }

    /// <summary>
    /// Enables idempotency for integration event handlers using Redis-only (non-transactional) guarantees.
    /// </summary>
    public RedisIdempotencyBuilder ForIntegrationEventHandlers()
    {
        _services.TryDecorate(typeof(IIntegrationEventHandler<>), (inner, serviceProvider) =>
        {
            var idempotencyService = serviceProvider.GetRequiredService<IIdempotencyService>();
            var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();

            var eventType = inner.GetType().GetInterfaces()
                .FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IIntegrationEventHandler<>))
                ?.GetGenericArguments()[0];

            if (eventType == null)
                return inner;

            var decoratorType = typeof(IdempotencyIntegrationEventDecorator<>).MakeGenericType(eventType);
            var logger = loggerFactory.CreateLogger(decoratorType);

            return Activator.CreateInstance(decoratorType, inner, idempotencyService, logger)!;
        });

        return this;
    }
}
