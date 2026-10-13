using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Sirius.Platform.Persistence.Options;

namespace Sirius.Platform.Persistence.Builders
{
    public static class Extensions
    {
        /// <summary>
        /// Registers a read-only DbContext derived from SiriusReadDbContext.
        /// The context uses NoTracking by default, carries no infrastructure tables, and throws on any write attempt.
        /// </summary>
        /// <typeparam name="TContext">The read-only DbContext type to register</typeparam>
        /// <param name="services">Service collection</param>
        /// <param name="configuration">Configuration action for SiriusReadDbContextConfiguration</param>
        /// <param name="dbOptions">Configuration action for DbContextOptionsBuilder (use read-only connection string)</param>
        /// <returns>Service collection for chaining</returns>
        public static IServiceCollection AddSiriusReadDbContext<TContext>(
            this IServiceCollection services,
            Action<SiriusReadDbContextConfiguration> configuration,
            Action<DbContextOptionsBuilder> dbOptions)
            where TContext : SiriusReadDbContext
        {
            var config = services.GetOrCreateSiriusReadDbContextConfiguration();
            configuration.Invoke(config);

            services.AddDbContext<TContext>((sp, options) =>
            {
                dbOptions.Invoke(options);
            });

            return services;
        }

        /// <summary>
        /// Registers a specific DbContext derived from DBContextBase with Sirius configuration.
        /// This method should be used for each service's specific DbContext (AccountContext, IdentityContext, etc.)
        /// </summary>
        /// <typeparam name="TContext">The specific DbContext type to register</typeparam>
        /// <param name="services">Service collection</param>
        /// <param name="configuration">Configuration action for SiriusDbContextConfiguration</param>
        /// <param name="dbOptions">Configuration action for DbContextOptionsBuilder</param>
        /// <returns>Service collection for chaining</returns>
        public static IServiceCollection AddSiriusSDBContext<TContext>(this IServiceCollection services, Action<SiriusDbContextConfiguration> configuration, Action<DbContextOptionsBuilder> dbOptions)
            where TContext : SiriusDBContext
        {

            var config = services.GetOrCreateSiriusDbContextConfiguration();
            configuration.Invoke(config);

            // Register the specific DbContext (NOT the base class)
            services.AddDbContext<TContext>((sp, options) =>
            {
                dbOptions.Invoke(options);
            });

            services.AddScoped<SiriusDBContext>(sp => sp.GetRequiredService<TContext>());
            services.AddScoped<ITransactionalUnitOfWork>(sp => sp.GetRequiredService<TContext>());
            services.AddScoped<IDomainEntityChangeTracker>(sp => sp.GetRequiredService<TContext>());

            return services;
        }
        /// <summary>
        /// Gets or creates SiriusDbContextConfiguration from the service collection.
        /// If it doesn't exist, creates a new instance and registers it as singleton.
        /// If it exists, returns the existing configuration to be modified.
        /// </summary>
        /// <param name="services">Service collection</param>
        /// <returns>The SiriusDbContextConfiguration instance</returns>
        internal static SiriusDbContextConfiguration GetOrCreateSiriusDbContextConfiguration(this IServiceCollection services)
        {
            var descriptor = services.FirstOrDefault(d => d.ServiceType == typeof(SiriusDbContextConfiguration));

            if (descriptor != null)
            {
                if (descriptor.ImplementationInstance is SiriusDbContextConfiguration existingConfig)
                {
                    return existingConfig;
                }

                services.Remove(descriptor);
            }

            var config = new SiriusDbContextConfiguration();
            services.AddSingleton(config);
            return config;
        }

        internal static SiriusReadDbContextConfiguration GetOrCreateSiriusReadDbContextConfiguration(this IServiceCollection services)
        {
            var descriptor = services.FirstOrDefault(d => d.ServiceType == typeof(SiriusReadDbContextConfiguration));

            if (descriptor != null)
            {
                if (descriptor.ImplementationInstance is SiriusReadDbContextConfiguration existingConfig)
                {
                    return existingConfig;
                }

                services.Remove(descriptor);
            }

            var config = new SiriusReadDbContextConfiguration();
            services.AddSingleton(config);
            return config;
        }

    }
}
