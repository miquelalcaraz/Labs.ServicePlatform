using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using QltS.Core.Persistence.EF.Extensions;

namespace QltS.Core.Persistence.EF.Relational.Extensions
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
        public static IServiceCollection AddQltSDBContext<TContext>(
            this IServiceCollection services,
            Action<QltDbContextConfiguration> configuration,
            Action<DbContextOptionsBuilder> dbOptions)
            where TContext : DBContextBase
        {
            // Get or create QltDbContextConfiguration
            var config = services.GetOrCreateQltDbContextConfiguration();

            // Apply configuration
            configuration.Invoke(config);

            // Register the specific DbContext (NOT the base class)
            services.AddDbContext<TContext>((sp, options) =>
            {
                dbOptions.Invoke(options);
            });

            //Register the specific instance as a generic context one. These will always be the same instance.
            services.AddScoped<DBContextBase>(sp => sp.GetRequiredService<TContext>());
            return services;
        }
    }
}
