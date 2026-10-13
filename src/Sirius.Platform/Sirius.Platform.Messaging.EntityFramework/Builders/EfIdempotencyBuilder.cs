using System;
using System.Linq;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Sirius.Platform.Messaging.IntegrationEvents;
using Sirius.Platform.Messaging.Services;
using Sirius.Platform.Persistence;

namespace Sirius.Platform.Messaging.Builders;

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
    /// When Redis idempotency is also registered, the Redis decorator is unwrapped and replaced
    /// by the EF decorator, since the hybrid IIdempotencyService already provides Redis fast-path checks.
    /// </summary>
    public EfIdempotencyBuilder<TContext> ForIntegrationEventHandlers()
    {
        EnsureIdempotencyServiceRegistered();

        _services.TryDecorate(typeof(IIntegrationEventHandler<>), (inner, serviceProvider) =>
        {
            var idempotencyService = serviceProvider.GetRequiredService<IIdempotencyService>();
            var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
            var unitOfWork = serviceProvider.GetService<TContext>() as ITransactionalUnitOfWork;

            var actualInner = UnwrapRedisIdempotencyDecorator(inner);

            var eventType = actualInner.GetType().GetInterfaces()
                .FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IIntegrationEventHandler<>))
                ?.GetGenericArguments()[0];

            if (eventType == null)
                return inner;

            var decoratorType = typeof(IdempotencyIntegrationEventEfDecorator<>).MakeGenericType(eventType);
            var logger = loggerFactory.CreateLogger(decoratorType);

            return Activator.CreateInstance(decoratorType, actualInner, idempotencyService, logger, unitOfWork)!;
        });

        return this;
    }

    private static object UnwrapRedisIdempotencyDecorator(object handler)
    {
        var handlerType = handler.GetType();

        if (!handlerType.IsGenericType)
            return handler;

        if (handlerType.GetGenericTypeDefinition() != typeof(IdempotencyIntegrationEventDecorator<>))
            return handler;

        var decoratedInterface = handlerType.GetInterfaces()
            .FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IDecoratedHandler<>));

        if (decoratedInterface == null)
            return handler;

        var innerProperty = handlerType.GetProperty(nameof(IDecoratedHandler<IntegrationEvent>.InnerHandler));

        return innerProperty?.GetValue(handler) ?? handler;
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

        var config = _services.GetOrCreateSiriusDbContextConfiguration();
        config.WithIdempotency();
    }
}
