using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Qlt.Platform.Messaging.Options;
using Qlt.Platform.Messaging.Services;
using Qlt.Platform.Persistence;
using Qlt.Platform.Persistence.Options;

using System;
using System.Linq;

namespace Qlt.Platform.Messaging.Builders
{
    public static class ServiceCollectionExtensions
    {
        /// <summary>
        /// Configures Entity Framework-based idempotency and returns a fluent builder for further configuration.
        /// </summary>
        public static EfIdempotencyBuilder<TContext> AddIdempotencyEntityFramework<TContext>(this IServiceCollection services)
            where TContext : DbContext, ITransactionalUnitOfWork
        {
            return new EfIdempotencyBuilder<TContext>(services);
        }

        /// <summary>
        /// Registers Transactional Outbox services.
        /// </summary>
        public static IServiceCollection AddTransactionalOutbox<TContext>(this IServiceCollection services, Action<OutboxMessageOptions> configure, Action<OutboxProcessorOptions>? configureProcessor = null)
            where TContext : DbContext, ITransactionalUnitOfWork
        {
            services.Configure(configure);

            services.AddScoped<IOutboxMessageService, OutboxMessageService<TContext>>();
            services.AddScoped<IEventBusOutboxService, EventBusOutboxService<TContext>>();
            services.AddOutboxProcessor(configureProcessor);

            QltDbContextConfiguration dbConfiguration = services.GetOrCreateQltDbContextConfiguration();
            dbConfiguration.WithTransactionalOutbox();
            return services;
        }

        /// <summary>
        /// Registers the background outbox processor that periodically publishes unpublished or failed events.
        /// Call this after <see cref="AddTransactionalOutbox{TContext}"/> to enable automatic retry of failed outbox events.
        /// </summary>
        /// <param name="services">The service collection</param>
        /// <param name="configure">Optional configuration for polling interval, batch size, and max retries</param>
        /// <returns>The service collection for chaining</returns>
        private static IServiceCollection AddOutboxProcessor(this IServiceCollection services, Action<OutboxProcessorOptions>? configure = null)
        {
            if (configure is not null)
            {
                services.Configure(configure);
            }
            else
            {
                services.AddOptions<OutboxProcessorOptions>();
            }

            services.AddHostedService<OutboxProcessor>();
            return services;
        }

        /// <summary>
        /// Gets or creates QltDbContextConfiguration from the service collection.
        /// </summary>
        internal static QltDbContextConfiguration GetOrCreateQltDbContextConfiguration(this IServiceCollection services)
        {
            var descriptor = services.FirstOrDefault(d => d.ServiceType == typeof(QltDbContextConfiguration));

            if (descriptor != null)
            {
                if (descriptor.ImplementationInstance is QltDbContextConfiguration existingConfig)
                {
                    return existingConfig;
                }

                services.Remove(descriptor);
            }

            var config = new QltDbContextConfiguration();
            services.AddSingleton(config);
            return config;
        }
    }
}
