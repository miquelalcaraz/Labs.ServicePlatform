using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using QltS.Shared.Messaging.Idempotency;
using QltS.Shared.Messaging.Inbox;
using QltS.Shared.Messaging.Outbox;
using QltS.Shared.Persistence.EF.Extensions;
using QltS.Shared.Persistence.EF.Idempotency;
using QltS.Shared.Persistence.EF.Inbox;
using QltS.Shared.Persistence.EF.Outbox;
using QltS.Shared.Persistence.EF.Relational.Configurations;

namespace QltS.Shared.Persistence.EF.Relational.Extensions
{
    public static class Extensions
    {
        /// <summary>
        /// Registers a specific DbContext derived from DBContextBase with QltS configuration.
        /// This method should be used for each service's specific DbContext (AccountContext, IdentityContext, etc.)
        /// </summary>
        /// <typeparam name="TContext">The specific DbContext type to register</typeparam>
        /// <param name="services">Service collection</param>
        /// <param name="configuration">Configuration action for QltDbContextConfiguration</param>
        /// <param name="dbOptions">Configuration action for DbContextOptionsBuilder</param>
        /// <returns>Service collection for chaining</returns>
        public static IServiceCollection AddQltSDBContext<TContext>(this IServiceCollection services, Action<QltDbContextConfiguration> configuration, Action<DbContextOptionsBuilder> dbOptions)
            where TContext : QltDBContextBase
        {

            var config = services.GetOrCreateQltDbContextConfiguration();
            configuration.Invoke(config);

            // Register the specific DbContext (NOT the base class)
            services.AddDbContext<TContext>((sp, options) =>
            {
                dbOptions.Invoke(options);
            });

            //Register the specific instance as a generic context one. These will always be the same instance.
            services.AddScoped<QltDBContextBase>(sp => sp.GetRequiredService<TContext>());

            if (config.UseRequestIdempotency)
            {
                services.AddScoped<IRequestIdempotencyService, RequestIdempotencyService<TContext>>();
            }
            if (config.UseAuditLogs)
            {
                //services.AddAuditLoggingCore<TContext>();
            }
            if (config.UseIntegrationEventInbox)
            {
                services.AddScoped<IIntegrationEventInbox, IntegrationEventInboxService<TContext>>();
            }
            if (config.UseTransactionalOutbox)
            {
                services.AddTransactionalOutbox<TContext>(options =>
                {
                    options.RegisterEventTypes(config.EventTypeAssembliesToRegister.ToArray());
                });
            }
            return services;
        }

        internal static IServiceCollection AddTransactionalOutbox<TContext>(this IServiceCollection services, Action<OutboxMessageOptions> configure)
            where TContext : QltDBContextBase
        {
            services.Configure(configure);
            services.AddScoped<IOutboxMessageService, OutboxMessageService<TContext>>();
            services.AddScoped<IEventBusOutboxService, EventBusOutboxService<TContext>>();


            return services;
        }

        /// <summary>
        /// Gets or creates QltDbContextConfiguration from the service collection.
        /// If it doesn't exist, creates a new instance and registers it as singleton.
        /// If it exists, returns the existing configuration to be modified.
        /// </summary>
        /// <param name="services">Service collection</param>
        /// <returns>The QltDbContextConfiguration instance</returns>
        internal static QltDbContextConfiguration GetOrCreateQltDbContextConfiguration(this IServiceCollection services)
        {
            // Try to find existing registration
            var descriptor = services.FirstOrDefault(d => d.ServiceType == typeof(QltDbContextConfiguration));

            if (descriptor != null)
            {
                // If it exists as a singleton instance, return it
                if (descriptor.ImplementationInstance is QltDbContextConfiguration existingConfig)
                {
                    return existingConfig;
                }

                // If it exists but not as instance (factory or type), remove it
                services.Remove(descriptor);
            }

            // Create new configuration and register as singleton
            var config = new QltDbContextConfiguration();
            services.AddSingleton(config);
            return config;
        }

    }
}
