using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Qlt.Platform.Persistence.Extensions
{
    public static class Extensions
    {
        /// <summary>
        /// Registers a specific DbContext derived from DBContextBase with Qlt configuration.
        /// This method should be used for each service's specific DbContext (AccountContext, IdentityContext, etc.)
        /// </summary>
        /// <typeparam name="TContext">The specific DbContext type to register</typeparam>
        /// <param name="services">Service collection</param>
        /// <param name="configuration">Configuration action for QltDbContextConfiguration</param>
        /// <param name="dbOptions">Configuration action for DbContextOptionsBuilder</param>
        /// <returns>Service collection for chaining</returns>
        public static IServiceCollection AddQltSDBContext<TContext>(this IServiceCollection services, Action<QltDbContextConfiguration> configuration, Action<DbContextOptionsBuilder> dbOptions)
            where TContext : QltDBContext
        {

            var config = services.GetOrCreateQltDbContextConfiguration();
            configuration.Invoke(config);

            // Register the specific DbContext (NOT the base class)
            services.AddDbContext<TContext>((sp, options) =>
            {
                dbOptions.Invoke(options);
            });

            //Register the specific instance as a generic context one. These will always be the same instance.
            services.AddScoped<QltDBContext>(sp => sp.GetRequiredService<TContext>());
            services.AddScoped<ITransactionalUnitOfWork>(sp => sp.GetRequiredService<TContext>());

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
