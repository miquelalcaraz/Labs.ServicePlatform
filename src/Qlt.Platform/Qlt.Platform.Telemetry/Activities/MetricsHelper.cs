using Qlt.Platform.Meters;

using System.Collections.Generic;
namespace Qlt.Platform.Activities;

/// <summary>
/// Helper class for recording OpenTelemetry metrics in handlers.
/// Centralizes metric recording operations to reduce code duplication across base classes.
/// </summary>
public static class MetricsHelper
{
    /// <summary>
    /// Records command handler execution metrics.
    /// </summary>
    /// <param name="commandType">The command type name</param>
    /// <param name="handlerType">The handler type name</param>
    /// <param name="durationMs">The execution duration in milliseconds</param>
    /// <param name="success">Whether the execution was successful</param>
    public static void RecordCommandHandlerExecution(
        string commandType,
        string handlerType,
        double durationMs,
        bool success)
    {
        var successTag = success.ToString().ToLowerInvariant();

        ApplicationDiagnostics.CommandHandlersProcessed.Add(1,
            new KeyValuePair<string, object?>("command_type", commandType),
            new KeyValuePair<string, object?>("handler_type", handlerType),
            new KeyValuePair<string, object?>("success", successTag));

        ApplicationDiagnostics.CommandHandlerDuration.Record(durationMs,
            new KeyValuePair<string, object?>("command_type", commandType),
            new KeyValuePair<string, object?>("handler_type", handlerType));
    }

    /// <summary>
    /// Records domain event handler execution metrics.
    /// </summary>
    /// <param name="eventType">The event type name</param>
    /// <param name="handlerType">The handler type name</param>
    /// <param name="durationMs">The execution duration in milliseconds</param>
    /// <param name="success">Whether the execution was successful</param>
    public static void RecordDomainEventHandlerExecution(
        string eventType,
        string handlerType,
        double durationMs,
        bool success)
    {
        var successTag = success.ToString().ToLowerInvariant();

        ApplicationDiagnostics.DomainEventHandlersProcessed.Add(1,
            new KeyValuePair<string, object?>("event_type", eventType),
            new KeyValuePair<string, object?>("handler_type", handlerType),
            new KeyValuePair<string, object?>("success", successTag));

        ApplicationDiagnostics.DomainEventHandlerDuration.Record(durationMs,
            new KeyValuePair<string, object?>("event_type", eventType),
            new KeyValuePair<string, object?>("handler_type", handlerType));
    }

    /// <summary>
    /// Records integration event handler execution metrics.
    /// </summary>
    /// <param name="eventType">The event type name</param>
    /// <param name="handlerType">The handler type name</param>
    /// <param name="durationMs">The execution duration in milliseconds</param>
    /// <param name="success">Whether the execution was successful</param>
    public static void RecordIntegrationEventHandlerExecution(
        string eventType,
        string handlerType,
        double durationMs,
        bool success)
    {
        var successTag = success.ToString().ToLowerInvariant();

        ApplicationDiagnostics.IntegrationEventHandlersProcessed.Add(1,
            new KeyValuePair<string, object?>("event_type", eventType),
            new KeyValuePair<string, object?>("handler_type", handlerType),
            new KeyValuePair<string, object?>("success", successTag));

        ApplicationDiagnostics.IntegrationEventHandlerDuration.Record(durationMs,
            new KeyValuePair<string, object?>("event_type", eventType),
            new KeyValuePair<string, object?>("handler_type", handlerType));
    }
}
