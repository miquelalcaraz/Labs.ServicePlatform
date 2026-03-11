using System;
using System.Reflection;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using QltSystem.Core.EventBus.Services;
using QltSystem.Core.IntegrationEventLogEF.Configuration;
using QltSystem.Core.IntegrationEventLogEF.Services;

namespace QltSystem.Core.IntegrationEventLogEF.Extensions;

/// <summary>
/// Extension methods for registering IntegrationEventLogService with DI container.
/// This registration scans and caches integration event types at startup instead of using reflection on each service instantiation.
/// </summary>
/// <example>
/// // Option 1: Action configuration (most flexible)
/// services.AddIntegrationEventLogService&lt;AccountContext&gt;(options => 
/// {
///     options.RegisterEventTypes(typeof(Program).Assembly);
/// });
/// 
/// // Option 2: Convenient shorthand
/// services.AddIntegrationEventLogService&lt;AccountContext&gt;(typeof(Program).Assembly);
/// 
/// // Option 3: With custom IIntegrationEventService
/// services.AddIntegrationEventLogService&lt;AccountContext, AccountIntegrationEventService&gt;(options =>
/// {
///     options.RegisterEventTypes(typeof(Program).Assembly);
/// });
/// 
/// // Option 4: Multiple assemblies
/// services.AddIntegrationEventLogService&lt;AccountContext&gt;(
///     typeof(Assembly1).Assembly,
///     typeof(Assembly2).Assembly
/// );
/// </example>
public static class ServiceCollectionExtensions
{
    #region Core Registration Method

    /// <summary>
    /// Registers IntegrationEventLogService with custom configuration.
    /// This is the core method that all other overloads call.
    /// </summary>
    /// <typeparam name="TContext">The DbContext type used for event log storage.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Action to configure integration event log options.</param>
    /// <returns>The service collection for chaining.</returns>
    /// <example>
    /// services.AddIntegrationEventLogService&lt;AccountContext&gt;(options =>
    /// {
    ///     options.RegisterEventTypes(typeof(Program).Assembly);
    /// });
    /// </example>
    public static IServiceCollection AddIntegrationEventLogService<TContext>(
        this IServiceCollection services,
        Action<IntegrationEventLogOptions> configure)
        where TContext : DbContext
    {
        services.Configure(configure);
        services.AddTransient<IIntegrationEventLogService, IntegrationEventLogService<TContext>>();

        return services;
    }

    #endregion

    #region Convenient Overloads - Assembly Scanning

    public static IServiceCollection AddIntegrationEventLogService<TContext>(
        this IServiceCollection services,
        Assembly assemblyContainingEvents)
        where TContext : DbContext
    {
        return services.AddIntegrationEventLogService<TContext>(options =>
        {
            options.RegisterEventTypes(assemblyContainingEvents);
        });
    }

    public static IServiceCollection AddIntegrationEventLogService<TContext>(
        this IServiceCollection services,
        params Assembly[] assembliesContainingEvents)
        where TContext : DbContext
    {
        return services.AddIntegrationEventLogService<TContext>(options =>
        {
            options.RegisterEventTypes(assembliesContainingEvents);
        });
    }

    public static IServiceCollection AddIntegrationEventLogService<TContext>(
        this IServiceCollection services,
        params Type[] eventTypes)
        where TContext : DbContext
    {
        return services.AddIntegrationEventLogService<TContext>(options =>
        {
            options.RegisterEventTypes(eventTypes);
        });
    }

    #endregion

    #region Overloads with Custom IIntegrationEventService

    /// <summary>
    /// Registers IntegrationEventLogService with custom configuration and a custom IIntegrationEventService implementation.
    /// </summary>
    /// <typeparam name="TContext">The DbContext type used for event log storage.</typeparam>
    /// <typeparam name="TIntegrationEventService">The custom IIntegrationEventService implementation.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Action to configure integration event log options.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddIntegrationEventLogService<TContext, TIntegrationEventService>(
        this IServiceCollection services,
        Action<IntegrationEventLogOptions> configure)
        where TContext : DbContext
        where TIntegrationEventService : class, IIntegrationEventService
    {
        services.AddIntegrationEventLogService<TContext>(configure);
        services.AddTransient<IIntegrationEventService, TIntegrationEventService>();

        return services;
    }

    public static IServiceCollection AddIntegrationEventLogService<TContext, TIntegrationEventService>(
        this IServiceCollection services,
        Assembly assemblyContainingEvents)
        where TContext : DbContext
        where TIntegrationEventService : class, IIntegrationEventService
    {
        return services.AddIntegrationEventLogService<TContext, TIntegrationEventService>(options =>
        {
            options.RegisterEventTypes(assemblyContainingEvents);
        });
    }

    public static IServiceCollection AddIntegrationEventLogService<TContext, TIntegrationEventService>(
        this IServiceCollection services,
        params Assembly[] assembliesContainingEvents)
        where TContext : DbContext
        where TIntegrationEventService : class, IIntegrationEventService
    {
        return services.AddIntegrationEventLogService<TContext, TIntegrationEventService>(options =>
        {
            options.RegisterEventTypes(assembliesContainingEvents);
        });
    }

    public static IServiceCollection AddIntegrationEventLogService<TContext, TIntegrationEventService>(
        this IServiceCollection services,
        params Type[] eventTypes)
        where TContext : DbContext
        where TIntegrationEventService : class, IIntegrationEventService
    {
        return services.AddIntegrationEventLogService<TContext, TIntegrationEventService>(options =>
        {
            options.RegisterEventTypes(eventTypes);
        });
    }

    #endregion
}
