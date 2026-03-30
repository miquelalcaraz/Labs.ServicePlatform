using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Qlt.Platform.Messaging.Extensions;
using Qlt.Platform.Messaging.Idempotency;
using Qlt.Platform.Messaging.IntegrationEvents;
using Qlt.Platform.Messaging.Services;
using Qlt.Platform.Persistence;

using System;
using System.Linq;

namespace Qlt.Platform.Messaging.Builders;

/// <summary>
/// Fluent builder for configuring Entity Framework-based idempotency.
/// </summary>
public class EfIdempotencyBuilder<TContext>
    where TContext : DbContext, ITransactionalUnitOfWork
{
    private readonly IServiceCollection _services;
    private bool _idempotencyServiceRegistered;

    internal EfIdempotencyBuilder(IServiceCollection services)
    {
        _services = services;
    }

    /// <summary>
    /// Enables idempotency for API command requests (MediatR pipeline).
    /// Registers EF-based IIdempotencyService, decorating Redis if already present.
    /// </summary>
    public EfIdempotencyBuilder<TContext> ForApiRequests()
    {
        EnsureIdempotencyServiceRegistered();
        return this;
    }

    /// <summary>
    /// Enables idempotency for integration event handlers using transactional guarantees.
    /// </summary>
    public EfIdempotencyBuilder<TContext> ForIntegrationEventHandlers()
    {
        EnsureIdempotencyServiceRegistered();

        _services.TryDecorate(typeof(IIntegrationEventHandler<>), (inner, serviceProvider) =>
        {
            var idempotencyService = serviceProvider.GetRequiredService<IIdempotencyService>();
            var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
            var dbContext = serviceProvider.GetService<TContext>();

            var eventType = inner.GetType().GetInterfaces()
                .FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IIntegrationEventHandler<>))
                ?.GetGenericArguments()[0];

            if (eventType == null)
                return inner;

            var decoratorType = typeof(IdempotencyIntegrationEventEfDecorator<>).MakeGenericType(eventType);
            var logger = loggerFactory.CreateLogger(decoratorType);

            return Activator.CreateInstance(decoratorType, inner, idempotencyService, logger, dbContext)!;
        });

        return this;
    }

    private void EnsureIdempotencyServiceRegistered()
    {
        if (_idempotencyServiceRegistered)
            return;

        _idempotencyServiceRegistered = true;

        var isRedisRegistered = _services.Any(d => d.ServiceType == typeof(IIdempotencyService));

        if (isRedisRegistered)
        {
            _services.Decorate<IIdempotencyService>((inner, sp) =>
            {
                var context = sp.GetRequiredService<TContext>();
                var logger = sp.GetRequiredService<ILogger<IdempotencyEfService<TContext>>>();
                return new IdempotencyEfService<TContext>(context, inner, logger);
            });
        }
        else
        {
            _services.AddScoped<IIdempotencyService>(sp =>
            {
                var context = sp.GetRequiredService<TContext>();
                var logger = sp.GetRequiredService<ILogger<IdempotencyEfService<TContext>>>();
                return new IdempotencyEfService<TContext>(context, logger);
            });
        }

        var config = _services.GetOrCreateQltDbContextConfiguration();
        config.WithIdempotency();
    }
}
