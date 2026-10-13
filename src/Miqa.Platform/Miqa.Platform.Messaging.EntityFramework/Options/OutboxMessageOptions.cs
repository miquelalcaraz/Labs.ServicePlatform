using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

using Miqa.Platform.Messaging.IntegrationEvents;

namespace Miqa.Platform.Messaging.Options;

/// <summary>
/// Configuration options for IntegrationEventLogService.
/// Use this to register integration event types at application startup.
/// </summary>
public class OutboxMessageOptions
{
    public IReadOnlyCollection<Type> EventTypes { get; private set; } = Array.Empty<Type>();

    /// <summary>
    /// Registers integration event types from an enumerable collection.
    /// </summary>
    /// <param name="eventTypes">The collection of integration event types to register.</param>
    public void RegisterEventTypes(IEnumerable<Type> eventTypes)
    {
        EventTypes = new List<Type>(eventTypes).AsReadOnly();
    }

    /// <summary>
    /// Registers integration event types from explicit type parameters.
    /// </summary>
    /// <param name="eventTypes">The integration event types to register.</param>
    public void RegisterEventTypes(params Type[] eventTypes)
    {
        RegisterEventTypes((IEnumerable<Type>)eventTypes);
    }

    /// <summary>
    /// Scans an assembly and registers all types that inherit from IntegrationEvent.
    /// </summary>
    /// <param name="assembly">The assembly to scan for integration events.</param>
    public void RegisterEventTypes(Assembly assembly)
    {
        var eventTypes = assembly
            .GetTypes()
            .Where(t => !t.IsAbstract && t.IsClass && t.IsAssignableTo(typeof(IntegrationEvent)))
            .ToArray();

        RegisterEventTypes(eventTypes);
    }

    /// <summary>
    /// Scans multiple assemblies and registers all types that inherit from IntegrationEvent.
    /// </summary>
    /// <param name="assemblies">The assemblies to scan for integration events.</param>
    public void RegisterEventTypes(params Assembly[] assemblies)
    {
        var eventTypes = assemblies
            .SelectMany(assembly => assembly.GetTypes())
            .Where(t => !t.IsAbstract && t.IsClass && t.IsAssignableTo(typeof(IntegrationEvent)))
            .ToArray();

        RegisterEventTypes(eventTypes);
    }
}
