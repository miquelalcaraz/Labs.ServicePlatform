using System.Reflection;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using QltSystem.Core.EventBus.Services;
using QltSystem.Core.EventBus.TransactionalOutbox.Configuration;
using QltSystem.Core.EventBus.TransactionalOutbox.Services;
using QltSystem.Core.Persistence.EF;
using QltSystem.Core.Persistence.EF.Extensions;

namespace QltSystem.Core.EventBus.TransactionalOutbox.Extensions;

/// <summary>
/// Extension methods for registering transactional outbox services with DI container.
/// Provides isolated transactional outbox functionality that can be optionally added to any service.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Gets or creates QltDbContextConfiguration from the service collection.
    /// If it doesn't exist, creates a new instance and registers it as singleton.
    /// If it exists, returns the existing configuration to be modified.
    /// </summary>
    private static QltDbContextConfiguration GetOrCreateQltDbContextConfiguration(this IServiceCollection services)
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

    /// <summary>
    /// Registers the transactional outbox service for a specific DbContext.
    /// This enables reliable integration event publishing using the Transactional Outbox pattern.
    /// </summary>
    /// <typeparam name="TContext">The DbContext type used for event log storage.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Action to configure integration event log options.</param>
    /// <returns>The service collection for chaining.</returns>
    /// <remarks>
    /// This registration provides:
    /// - IIntegrationEventLogService implementation using the specified DbContext
    /// - IIntegrationEventService implementation for publishing events
    /// - Stores integration events in IntegrationEventLog table
    /// - Ensures events are published within the same transaction as business data
    /// 
    /// The configuration is automatically applied when using DBContextBase and registering
    /// the EventBus.TransactionalOutbox assembly.
    /// 
    /// For convenience, this method automatically registers the assembly in the configuration
    /// by getting or creating QltDbContextConfiguration if it doesn't exist yet.
    /// </remarks>
    /// <example>
    /// services.AddTransactionalOutbox&lt;AccountContext&gt;(options =>
    /// {
    ///     options.RegisterEventTypes(typeof(Program).Assembly);
    /// });
    /// </example>
    public static IServiceCollection AddTransactionalOutbox<TContext>(
        this IServiceCollection services,
        Action<IntegrationEventLogOptions> configure)
        where TContext : DBContextBase
    {
        services.Configure(configure);
        services.AddScoped<IIntegrationEventLogService, IntegrationEventLogService<TContext>>();
        services.AddScoped<IIntegrationEventService, IntegrationEventService<TContext>>();

        // Get or create QltDbContextConfiguration and register the EventBus.TransactionalOutbox assembly
        var config = services.GetOrCreateQltDbContextConfiguration();
        var outboxAssembly = typeof(IntegrationEventLogConfiguration).Assembly;

        if (!config.IsAssemblyRegistered(outboxAssembly))
        {
            config.RegisterModelsFromAssembly(outboxAssembly);
        }

        return services;
    }

    #region Convenient Overloads

    /// <summary>
    /// Registers the transactional outbox service with assembly scanning for event types.
    /// </summary>
    public static IServiceCollection AddTransactionalOutbox<TContext>(
        this IServiceCollection services,
        Assembly assemblyContainingEvents)
        where TContext : DBContextBase
    {
        return services.AddTransactionalOutbox<TContext>(options =>
        {
            options.RegisterEventTypes(assemblyContainingEvents);
        });
    }

    /// <summary>
    /// Registers the transactional outbox service with multiple assemblies for event type scanning.
    /// </summary>
    public static IServiceCollection AddTransactionalOutbox<TContext>(
        this IServiceCollection services,
        params Assembly[] assembliesContainingEvents)
        where TContext : DBContextBase
    {
        return services.AddTransactionalOutbox<TContext>(options =>
        {
            options.RegisterEventTypes(assembliesContainingEvents);
        });
    }

    /// <summary>
    /// Registers the transactional outbox service with explicit event types.
    /// </summary>
    public static IServiceCollection AddTransactionalOutbox<TContext>(
        this IServiceCollection services,
        params Type[] eventTypes)
        where TContext : DBContextBase
    {
        return services.AddTransactionalOutbox<TContext>(options =>
        {
            options.RegisterEventTypes(eventTypes);
        });
    }

    #endregion
}
