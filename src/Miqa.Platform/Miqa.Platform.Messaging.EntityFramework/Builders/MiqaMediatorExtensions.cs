using System;
using System.Linq;

using Microsoft.Extensions.DependencyInjection;

using Miqa.Platform.Messaging.Behaviors;

namespace Miqa.Platform.Messaging.Builders;

/// <summary>
/// Registers the standard Miqa MediatR pipeline with automatic telemetry.
/// </summary>
public static class MiqaMediatorExtensions
{
    /// <summary>
    /// Configures MediatR with the standard Miqa pipeline behaviors and automatic telemetry.
    /// Pipeline order: Logging → Idempotency → IntegrationEventProcessor → Transaction → Validation → DomainEventProcessor.
    /// Telemetry for commands, queries, domain events, and integration events is always included.
    /// </summary>
    public static IServiceCollection AddMiqaMediator(
        this IServiceCollection services,
        Action<MiqaMediatorConfiguration> configure)
    {
        if (services.Any(d => d.ServiceType == typeof(MiqaMediatorMarker)))
            return services;

        services.AddSingleton<MiqaMediatorMarker>();

        var config = new MiqaMediatorConfiguration();
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

    private sealed class MiqaMediatorMarker;
}
