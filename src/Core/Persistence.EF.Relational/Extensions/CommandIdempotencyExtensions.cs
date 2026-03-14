using Microsoft.Extensions.DependencyInjection;

using QltSystem.Core.Persistence.EF;
using QltSystem.Core.Persistence.EF.Extensions;
using QltSystem.Core.Persistence.EF.Idempotency.Extensions;

namespace Core.Persistence.EF.Relational.Extensions;

/// <summary>
/// Extension methods for command idempotency in relational databases.
/// </summary>
public static class CommandIdempotencyExtensions
{
    /// <summary>
    /// Registers command idempotency services for relational databases.
    /// This includes RequestManager and ClientRequest entity configuration for relational DBs.
    /// </summary>
    /// <typeparam name="TContext">The DbContext type</typeparam>
    /// <param name="services">The service collection</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddCommandIdempotency<TContext>(this IServiceCollection services)
        where TContext : DBContextBase
    {
        // Register the base command idempotency services (RequestManager)
        services.AddCommandIdempotencyCore<TContext>();

        // Register the relational assembly for ClientRequestConfiguration discovery
        var configuration = services.GetOrCreateQltDbContextConfiguration();
        var relationalAssembly = typeof(QltDBContext).Assembly;

        if (!configuration.IsAssemblyRegistered(relationalAssembly))
        {
            configuration.RegisterModelsFromAssembly(relationalAssembly);
        }

        return services;
    }
}
