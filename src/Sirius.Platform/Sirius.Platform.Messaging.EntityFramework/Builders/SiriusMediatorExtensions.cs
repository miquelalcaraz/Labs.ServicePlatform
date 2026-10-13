using System;
using System.Linq;

using Microsoft.Extensions.DependencyInjection;

using Sirius.Platform.Messaging.Behaviors;

namespace Sirius.Platform.Messaging.Builders;

/// <summary>
/// Registers the standard Sirius MediatR pipeline with automatic telemetry.
/// </summary>
public static class SiriusMediatorExtensions
{
    /// <summary>
    /// Configures MediatR with the standard Sirius pipeline behaviors and automatic telemetry.
    /// Pipeline order: Logging → Idempotency → IntegrationEventProcessor → Transaction → Validation → DomainEventProcessor.
    /// Telemetry for commands, queries, domain events, and integration events is always included.
    /// </summary>
    public static IServiceCollection AddSiriusMediator(
        this IServiceCollection services,
        Action<SiriusMediatorConfiguration> configure)
    {
        if (services.Any(d => d.ServiceType == typeof(SiriusMediatorMarker)))
            return services;

        services.AddSingleton<SiriusMediatorMarker>();

        var config = new SiriusMediatorConfiguration();
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

    private sealed class SiriusMediatorMarker;
}
