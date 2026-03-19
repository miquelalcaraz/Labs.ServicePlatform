using Microsoft.Extensions.DependencyInjection;

using QltS.Core.EventBus.Idempotency;

namespace QltS.Core.Persistence.EF.Idempotency.Extensions;

/// <summary>
/// Internal extension methods for registering command idempotency services.
/// Use the specific implementation (Relational, Cosmos, etc.) for public API.
/// </summary>
internal static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers core command idempotency services for the specified DbContext.
    /// This is an internal helper method. Use AddCommandIdempotency from specific 
    /// persistence implementations (Relational, Cosmos, etc.).
    /// </summary>
    /// <typeparam name="TContext">The DbContext type</typeparam>
    /// <param name="services">The service collection</param>
    /// <returns>The service collection for chaining</returns>
    internal static IServiceCollection AddCommandIdempotencyCore<TContext>(this IServiceCollection services)
        where TContext : DBContextBase
    {
        // Register the RequestManager
        services.AddScoped<IRequestManager, RequestManager<TContext>>();

        return services;
    }
}
