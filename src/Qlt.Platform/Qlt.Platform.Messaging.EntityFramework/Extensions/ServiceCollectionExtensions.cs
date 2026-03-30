using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Qlt.Platform.Messaging.Builders;
using Qlt.Platform.Messaging.Outbox;
using Qlt.Platform.Messaging.Services;
using Qlt.Platform.Persistence;
using Qlt.Platform.Persistence.Extensions;

using System;
using System.Linq;

namespace Qlt.Platform.Messaging.Extensions
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
        public static IServiceCollection AddTransactionalOutbox<TContext>(this IServiceCollection services, Action<OutboxMessageOptions> configure)
            where TContext : DbContext, ITransactionalUnitOfWork
        {
            services.Configure(configure);
            services.AddScoped<IOutboxMessageService, OutboxMessageService<TContext>>();
            services.AddScoped<IEventBusOutboxService, EventBusOutboxService<TContext>>();

            QltDbContextConfiguration dbConfiguration = services.GetOrCreateQltDbContextConfiguration();
            dbConfiguration.WithTransactionalOutbox();
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
