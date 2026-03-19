namespace QltS.Core.EventBus.TransactionalOutbox.Extensions;

/// <summary>
/// Extension methods for configuring MediatR with Transactional Outbox pattern support.
/// </summary>
public static class MediatRExtensions
{
    /// <summary>
    /// Registers the TransactionBehavior required for the Transactional Outbox pattern.
    /// This behavior wraps commands in database transactions and publishes integration events after commit.
    /// </summary>
    /// <typeparam name="T">The MediatR service configuration type</typeparam>
    /// <param name="config">The MediatR service configuration</param>
    /// <returns>The MediatR service configuration for chaining</returns>
    public static T AddTransactionalOutboxBehavior<T>(this T config) where T : class
    {
        // Use dynamic to call AddOpenBehavior without reflection issues
        dynamic dynamicConfig = config;
        dynamicConfig.AddOpenBehavior(typeof(Behaviors.TransactionBehavior<,>));
        return config;
    }
}
