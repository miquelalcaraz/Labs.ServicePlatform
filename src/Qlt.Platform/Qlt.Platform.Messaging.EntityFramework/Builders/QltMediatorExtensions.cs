using Microsoft.Extensions.DependencyInjection;

using Qlt.Platform.Messaging.Behaviors;

using System;
using System.Linq;

namespace Qlt.Platform.Messaging.Builders;

/// <summary>
/// Registers the standard Qlt MediatR pipeline with automatic telemetry.
/// </summary>
public static class QltMediatorExtensions
{
    /// <summary>
    /// Configures MediatR with the standard Qlt pipeline behaviors and automatic telemetry.
    /// Pipeline order: Logging → Idempotency → IntegrationEventProcessor → Transaction → Validation → DomainEventProcessor.
    /// Telemetry for commands, queries, domain events, and integration events is always included.
    /// </summary>
    public static IServiceCollection AddQltMediator(
        this IServiceCollection services,
        Action<QltMediatorConfiguration> configure)
    {
        if (services.Any(d => d.ServiceType == typeof(QltMediatorMarker)))
            return services;

        services.AddSingleton<QltMediatorMarker>();

        var config = new QltMediatorConfiguration();
        configure(config);

        services.AddMediatR(cfg =>
        {
            foreach (var assembly in config.Assemblies)
                cfg.RegisterServicesFromAssembly(assembly);

            if (config.LoggingBehaviorType is not null)
                cfg.AddOpenBehavior(config.LoggingBehaviorType);

            cfg.AddOpenBehavior(typeof(IdempotencyBehavior<,>));
            cfg.AddOpenBehavior(typeof(IntegrationEventProcessorBehavior<,>));
            cfg.AddOpenBehavior(typeof(TransactionBehavior<,>));

            if (config.ValidationBehaviorType is not null)
                cfg.AddOpenBehavior(config.ValidationBehaviorType);

            cfg.AddOpenBehavior(typeof(DomainEventProcessorBehavior<,>));
        });

        services.AddIntegrationEventTelemetryDecorator();
        services.AddDomainEventTelemetryBehavior();
        services.AddCommandTelemetryBehavior();
        services.AddQueryTelemetryBehavior();

        return services;
    }

    private sealed class QltMediatorMarker;
}
