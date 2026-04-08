using MediatR;

using Microsoft.Extensions.DependencyInjection;

using Qlt.Platform.Messaging.Behaviors;
using Qlt.Platform.Messaging.IntegrationEvents;

using System;
using System.Linq;


namespace Qlt.Platform.Messaging.Builders;

/// <summary>
/// Extension methods for registering telemetry behaviors and decorators.
/// All methods are idempotent — safe to call multiple times without duplicate registrations.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Decorates all registered IIntegrationEventHandler implementations with automatic telemetry (OpenTelemetry activities and metrics).
    /// </summary>
    public static IServiceCollection AddIntegrationEventTelemetryDecorator(this IServiceCollection services)
    {
        if (services.Any(d => d.ServiceType == typeof(IntegrationEventTelemetryMarker)))
            return services;

        services.AddSingleton<IntegrationEventTelemetryMarker>();

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
    /// </summary>
    public static IServiceCollection AddDomainEventTelemetryBehavior(this IServiceCollection services)
    {
        if (services.Any(d => d.ServiceType == typeof(DomainEventTelemetryMarker)))
            return services;

        services.AddSingleton<DomainEventTelemetryMarker>();
        services.AddSingleton<INotificationPublisher, TelemetryDomainEventBehavior>();
        return services;
    }

    /// <summary>
    /// Registers TelemetryCommandBehavior as a MediatR pipeline behavior for automatic telemetry on command handlers.
    /// </summary>
    public static IServiceCollection AddCommandTelemetryBehavior(this IServiceCollection services)
    {
        if (services.Any(d => d.ServiceType == typeof(CommandTelemetryMarker)))
            return services;

        services.AddSingleton<CommandTelemetryMarker>();
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(TelemetryCommandBehavior<,>));
        return services;
    }

    /// <summary>
    /// Registers TelemetryQueryBehavior as a MediatR pipeline behavior for automatic telemetry on query handlers.
    /// </summary>
    public static IServiceCollection AddQueryTelemetryBehavior(this IServiceCollection services)
    {
        if (services.Any(d => d.ServiceType == typeof(QueryTelemetryMarker)))
            return services;

        services.AddSingleton<QueryTelemetryMarker>();
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(TelemetryQueryBehavior<,>));
        return services;
    }

    private sealed class IntegrationEventTelemetryMarker;
    private sealed class DomainEventTelemetryMarker;
    private sealed class CommandTelemetryMarker;
    private sealed class QueryTelemetryMarker;
}
