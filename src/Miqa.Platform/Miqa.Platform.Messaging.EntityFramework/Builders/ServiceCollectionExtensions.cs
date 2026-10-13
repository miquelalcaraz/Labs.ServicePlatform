using System;
using System.Linq;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Miqa.Platform.Messaging.Options;
using Miqa.Platform.Messaging.Services;
using Miqa.Platform.Persistence;
using Miqa.Platform.Persistence.Options;

namespace Miqa.Platform.Messaging.Builders
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

            MiqaDbContextConfiguration dbConfiguration = services.GetOrCreateMiqaDbContextConfiguration();
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
        /// Gets or creates MiqaDbContextConfiguration from the service collection.
        /// </summary>
        internal static MiqaDbContextConfiguration GetOrCreateMiqaDbContextConfiguration(this IServiceCollection services)
        {
            var descriptor = services.FirstOrDefault(d => d.ServiceType == typeof(MiqaDbContextConfiguration));

            if (descriptor != null)
            {
                if (descriptor.ImplementationInstance is MiqaDbContextConfiguration existingConfig)
                {
                    return existingConfig;
                }

                services.Remove(descriptor);
            }

            var config = new MiqaDbContextConfiguration();
            services.AddSingleton(config);
            return config;
        }
    }
}
