using System;
using System.Linq;

using MediatR;

using Microsoft.Extensions.DependencyInjection;

using Qlt.Platform.Messaging.Behaviors;
using Qlt.Platform.Messaging.IntegrationEvents;


namespace Qlt.Platform.Messaging.Builders;

/// <summary>
/// Extension methods for registering telemetry behaviors and decorators.
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
    /// Registers TelemetryDomainEventBehavior as MediatR's notification publisher for automatic telemetry on domain event handlers.
    /// Uses INotificationPublisher because it is MediatR's native extension point for notification cross-cutting concerns,
    /// equivalent to IPipelineBehavior for the Publish() path. Only adds telemetry for handlers implementing IDomainEventHandler.
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddDomainEventTelemetryBehavior(this IServiceCollection services)
    {
        services.AddSingleton<INotificationPublisher, TelemetryDomainEventBehavior>();
        return services;
    }

    /// <summary>
    /// Registers TelemetryCommandBehavior as a MediatR pipeline behavior for automatic telemetry on command handlers.
    /// Uses IPipelineBehavior because MediatR registers command handlers as IRequestHandler&lt;,&gt;, not ICommandHandler&lt;,&gt;,
    /// making Scrutor TryDecorate ineffective for MediatR-managed handlers.
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddCommandTelemetryBehavior(this IServiceCollection services)
    {
        services.AddTransient(typeof(MediatR.IPipelineBehavior<,>), typeof(TelemetryCommandBehavior<,>));
        return services;
    }

    /// <summary>
    /// Registers TelemetryQueryBehavior as a MediatR pipeline behavior for automatic telemetry on query handlers.
    /// Only activates for requests implementing IQuery&lt;TResponse&gt;; commands and other request types pass through.
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddQueryTelemetryBehavior(this IServiceCollection services)
    {
        services.AddTransient(typeof(MediatR.IPipelineBehavior<,>), typeof(TelemetryQueryBehavior<,>));
        return services;
    }
}
