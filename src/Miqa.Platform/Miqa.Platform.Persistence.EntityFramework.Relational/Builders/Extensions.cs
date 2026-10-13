using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Miqa.Platform.Persistence.Options;

namespace Miqa.Platform.Persistence.Builders
{
    public static class Extensions
    {
        /// <summary>
        /// Registers a read-only DbContext derived from MiqaReadDbContext.
        /// The context uses NoTracking by default, carries no infrastructure tables, and throws on any write attempt.
        /// </summary>
        /// <typeparam name="TContext">The read-only DbContext type to register</typeparam>
        /// <param name="services">Service collection</param>
        /// <param name="configuration">Configuration action for MiqaReadDbContextConfiguration</param>
        /// <param name="dbOptions">Configuration action for DbContextOptionsBuilder (use read-only connection string)</param>
        /// <returns>Service collection for chaining</returns>
        public static IServiceCollection AddMiqaReadDbContext<TContext>(
            this IServiceCollection services,
            Action<MiqaReadDbContextConfiguration> configuration,
            Action<DbContextOptionsBuilder> dbOptions)
            where TContext : MiqaReadDbContext
        {
            var config = services.GetOrCreateMiqaReadDbContextConfiguration();
            configuration.Invoke(config);

            services.AddDbContext<TContext>((sp, options) =>
            {
                dbOptions.Invoke(options);
            });

            return services;
        }

        /// <summary>
        /// Registers a specific DbContext derived from DBContextBase with Miqa configuration.
        /// This method should be used for each service's specific DbContext (AccountContext, IdentityContext, etc.)
        /// </summary>
        /// <typeparam name="TContext">The specific DbContext type to register</typeparam>
        /// <param name="services">Service collection</param>
        /// <param name="configuration">Configuration action for MiqaDbContextConfiguration</param>
        /// <param name="dbOptions">Configuration action for DbContextOptionsBuilder</param>
        /// <returns>Service collection for chaining</returns>
        public static IServiceCollection AddMiqaSDBContext<TContext>(this IServiceCollection services, Action<MiqaDbContextConfiguration> configuration, Action<DbContextOptionsBuilder> dbOptions)
            where TContext : MiqaDBContext
        {

            var config = services.GetOrCreateMiqaDbContextConfiguration();
            configuration.Invoke(config);

            // Register the specific DbContext (NOT the base class)
            services.AddDbContext<TContext>((sp, options) =>
            {
                dbOptions.Invoke(options);
            });

            services.AddScoped<MiqaDBContext>(sp => sp.GetRequiredService<TContext>());
            services.AddScoped<ITransactionalUnitOfWork>(sp => sp.GetRequiredService<TContext>());
            services.AddScoped<IDomainEntityChangeTracker>(sp => sp.GetRequiredService<TContext>());

            return services;
        }
        /// <summary>
        /// Gets or creates MiqaDbContextConfiguration from the service collection.
        /// If it doesn't exist, creates a new instance and registers it as singleton.
        /// If it exists, returns the existing configuration to be modified.
        /// </summary>
        /// <param name="services">Service collection</param>
        /// <returns>The MiqaDbContextConfiguration instance</returns>
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

        internal static MiqaReadDbContextConfiguration GetOrCreateMiqaReadDbContextConfiguration(this IServiceCollection services)
        {
            var descriptor = services.FirstOrDefault(d => d.ServiceType == typeof(MiqaReadDbContextConfiguration));

            if (descriptor != null)
            {
                if (descriptor.ImplementationInstance is MiqaReadDbContextConfiguration existingConfig)
                {
                    return existingConfig;
                }

                services.Remove(descriptor);
            }

            var config = new MiqaReadDbContextConfiguration();
            services.AddSingleton(config);
            return config;
        }

    }
}
