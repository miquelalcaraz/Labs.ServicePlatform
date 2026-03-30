using Microsoft.Extensions.DependencyInjection;

using Qlt.Platform.Messaging.DomainEvents;
using Qlt.Platform.Messaging.IntegrationEvents;

using System;
using System.Linq;


namespace Qlt.Platform.Messaging.Extensions;

/// <summary>
/// Extension methods for registering event handler decorators (integration events and domain events).
/// </summary>
public static class ServiceCollectionExtensions
{

    /// <summary>
    /// Decorates all registered IIntegrationEventHandler implementations with automatic telemetry (OpenTelemetry activities and metrics).
    /// This should be called AFTER registering handlers but BEFORE calling ForIntegrationEventHandlers() on idempotency builders to ensure telemetry measures the complete execution including idempotency checks.
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddIntegrationEventTelemetryDecorator(this IServiceCollection services)
    {
        // Register the decorator using Scrutor's Decorate pattern
        // TryDecorate returns false if no services are found, avoiding exception
        services.TryDecorate(typeof(IIntegrationEventHandler<>), (inner, serviceProvider) =>
        {
            var eventType = inner.GetType().GetInterfaces()
                .FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IIntegrationEventHandler<>))
                ?.GetGenericArguments()[0];

            if (eventType == null)
                return inner;

            var decoratorType = typeof(TelemetryIntegrationEventHandlerDecorator<>).MakeGenericType(eventType);

            return Activator.CreateInstance(decoratorType, inner)!;
        });

        return services;
    }

    /// <summary>
    /// Decorates all registered IDomainEventHandler implementations with automatic telemetry (OpenTelemetry activities and metrics).
    /// This should be called AFTER registering handlers to ensure telemetry wraps the complete execution.
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddDomainEventTelemetryDecorator(this IServiceCollection services)
    {
        // Register the decorator using Scrutor's Decorate pattern
        services.TryDecorate(typeof(IDomainEventHandler<>), (inner, serviceProvider) =>
        {
            var eventType = inner.GetType().GetInterfaces()
                .FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IDomainEventHandler<>))
                ?.GetGenericArguments()[0];

            if (eventType == null)
                return inner;

            var decoratorType = typeof(TelemetryDomainEventHandlerDecorator<>).MakeGenericType(eventType);

            return Activator.CreateInstance(decoratorType, inner)!;
        });

        return services;
    }




}
