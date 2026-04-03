using System;
using System.Collections.Generic;
using System.Reflection;

namespace Qlt.Platform.Messaging.Builders;

/// <summary>
/// Fluent configuration for the Qlt MediatR pipeline.
/// Platform behaviors (idempotency, transaction, outbox, domain events) and telemetry are registered automatically.
/// Only service-specific concerns (assemblies, logging, validation) need explicit configuration.
/// </summary>
public class QltMediatorConfiguration
{
    internal List<Assembly> Assemblies { get; } = [];
    internal Type? LoggingBehaviorType { get; private set; }
    internal Type? ValidationBehaviorType { get; private set; }

    /// <summary>
    /// Registers MediatR handlers from the specified assembly.
    /// </summary>
    public QltMediatorConfiguration RegisterServicesFromAssembly(Assembly assembly)
    {
        Assemblies.Add(assembly);
        return this;
    }

    /// <summary>
    /// Registers a logging pipeline behavior at the outermost position of the pipeline.
    /// Accepts an open generic type implementing IPipelineBehavior&lt;,&gt;.
    /// </summary>
    public QltMediatorConfiguration WithLogging(Type openBehaviorType)
    {
        ValidateOpenGenericBehavior(openBehaviorType, nameof(openBehaviorType));
        LoggingBehaviorType = openBehaviorType;
        return this;
    }

    /// <summary>
    /// Registers a validation pipeline behavior inside the transaction boundary.
    /// Accepts an open generic type implementing IPipelineBehavior&lt;,&gt;.
    /// </summary>
    public QltMediatorConfiguration WithValidation(Type openBehaviorType)
    {
        ValidateOpenGenericBehavior(openBehaviorType, nameof(openBehaviorType));
        ValidationBehaviorType = openBehaviorType;
        return this;
    }

    private static void ValidateOpenGenericBehavior(Type type, string paramName)
    {
        if (!type.IsGenericTypeDefinition || type.GetGenericArguments().Length != 2)
            throw new ArgumentException(
                $"Type must be an open generic with two type parameters (e.g., typeof(MyBehavior<,>)). Got: {type.FullName}",
                paramName);
    }
}
