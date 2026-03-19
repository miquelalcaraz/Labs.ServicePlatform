using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using QltS.Core.EventBus.Idempotency.Configuration;
using QltS.Core.EventBus.Idempotency.Services;
using QltS.Core.EventBus.Services;
using QltS.Core.Persistence.EF.Extensions;

namespace QltS.Core.EventBus.Idempotency.Extensions;

/// <summary>
/// Extension methods for registering event idempotency services with DI container.
/// Provides isolated idempotency functionality that can be optionally added to any service.
/// </summary>
/// <example>
/// // In your service configuration (using QltDbContextConfiguration):
/// services.AddQltSDBContext&lt;AccountContext&gt;(config =>
/// {
///     config.RegisterModelsFromAssembly(typeof(AccountContext).Assembly);
/// }, dbOptions =>
/// {
///     dbOptions.UseSqlServer(connectionString);
/// });
/// 
/// // Then add idempotency - it will automatically register the configuration
/// services.AddEventIdempotency&lt;AccountContext&gt;();
/// 
/// // OR with DBContextBase directly:
/// services.AddEventIdempotency&lt;AccountContext&gt;();
/// // The configuration will be automatically applied via ApplyConfigurationsFromAssembly
/// </example>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Gets or creates QltDbContextConfiguration from the service collection.
    /// If it doesn't exist, creates a new instance and registers it as singleton.
    /// If it exists, returns the existing configuration to be modified.
    /// </summary>
    /// <param name="services">Service collection</param>
    /// <returns>The QltDbContextConfiguration instance</returns>
    private static QltDbContextConfiguration GetOrCreateQltDbContextConfiguration(this IServiceCollection services)
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

    /// <summary>
    /// Registers the event idempotency service for a specific DbContext.
    /// This enables idempotent consumption of integration events.
    /// The configuration is automatically applied if the EventBus.Idempotency assembly 
    /// is registered in QltDbContextConfiguration.
    /// </summary>
    /// <typeparam name="TContext">The DbContext type used for idempotency tracking storage.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    /// <remarks>
    /// This registration provides:
    /// - IEventIdempotencyService implementation using the specified DbContext
    /// - Tracks processed events in ProcessedIntegrationEvents table
    /// - Prevents duplicate event processing by handlers
    /// 
    /// The configuration is automatically applied when using DBContextBase and registering
    /// the EventBus.Idempotency assembly via:
    /// config.RegisterModelsFromAssembly(typeof(ProcessedIntegrationEventConfiguration).Assembly)
    /// 
    /// For convenience, this method automatically registers the assembly in the configuration
    /// by getting or creating QltDbContextConfiguration if it doesn't exist yet.
    /// </remarks>
    public static IServiceCollection AddEventIdempotency<TContext>(this IServiceCollection services)
        where TContext : DbContext
    {
        services.AddScoped<IEventIdempotencyService, EventIdempotencyService<TContext>>();

        // Get or create QltDbContextConfiguration and register the EventBus.Idempotency assembly
        var config = services.GetOrCreateQltDbContextConfiguration();
        var idempotencyAssembly = typeof(ProcessedIntegrationEventConfiguration).Assembly;

        if (!config.IsAssemblyRegistered(idempotencyAssembly))
        {
            config.RegisterModelsFromAssembly(idempotencyAssembly);
        }

        return services;
    }
}
