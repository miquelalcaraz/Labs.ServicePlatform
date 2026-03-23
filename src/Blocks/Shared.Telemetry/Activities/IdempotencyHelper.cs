using QltS.Shared.Telemetry.Meters;

using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace QltS.Shared.Telemetry.Activities;

/// <summary>
/// Helper class for idempotency operations with built-in telemetry.
/// Centralizes idempotency check logic and metrics recording.
/// </summary>
public static class IdempotencyHelper
{
    /// <summary>
    /// Starts an idempotency check activity for commands.
    /// </summary>
    /// <param name="requestId">The request identifier</param>
    /// <param name="commandType">The command type name</param>
    /// <returns>The created activity</returns>
    public static Activity? StartCommandIdempotencyCheck(Guid requestId, string commandType)
    {
        var activity = ApplicationDiagnostics.ActivitySource.StartActivity(
            name: "Idempotency Check (Command)",
            kind: ActivityKind.Internal);

        activity?.SetTag("idempotency.request_id", requestId);
        activity?.SetTag("idempotency.command_type", commandType);
        activity?.SetTag("idempotency.check_type", "command");

        return activity;
    }

    /// <summary>
    /// Starts an idempotency check activity for events.
    /// </summary>
    /// <param name="eventId">The event identifier</param>
    /// <param name="handlerName">The handler name</param>
    /// <returns>The created activity</returns>
    public static Activity? StartEventIdempotencyCheck(Guid eventId, string handlerName)
    {
        var activity = ApplicationDiagnostics.ActivitySource.StartActivity(
            name: "Idempotency Check (Event)",
            kind: ActivityKind.Internal);

        activity?.SetTag("idempotency.event_id", eventId);
        activity?.SetTag("idempotency.handler_name", handlerName);
        activity?.SetTag("idempotency.check_type", "event");

        return activity;
    }

    /// <summary>
    /// Records the result of an idempotency check.
    /// </summary>
    /// <param name="activity">The activity to update</param>
    /// <param name="isDuplicate">Whether a duplicate was detected</param>
    /// <param name="checkDurationMs">The duration of the check in milliseconds</param>
    public static void RecordCheckResult(
        Activity? activity,
        bool isDuplicate,
        double checkDurationMs)
    {
        if (activity == null) return;

        activity.SetTag("idempotency.is_duplicate", isDuplicate);
        activity.SetTag("idempotency.check_duration_ms", checkDurationMs);
    }

    /// <summary>
    /// Records idempotency check duration metric.
    /// </summary>
    /// <param name="checkType">The type of check ("command" or "event")</param>
    /// <param name="isDuplicate">Whether a duplicate was detected</param>
    /// <param name="durationMs">The duration in milliseconds</param>
    public static void RecordCheckDurationMetric(
        string checkType,
        bool isDuplicate,
        double durationMs)
    {
        ApplicationDiagnostics.IdempotencyCheckDuration.Record(durationMs,
            new KeyValuePair<string, object?>("check_type", checkType),
            new KeyValuePair<string, object?>("is_duplicate", isDuplicate.ToString().ToLowerInvariant()));
    }

    /// <summary>
    /// Records a duplicate command detection.
    /// </summary>
    /// <param name="activity">The activity to update</param>
    /// <param name="commandType">The command type</param>
    /// <param name="requestId">The request identifier</param>
    public static void RecordDuplicateCommand(
        Activity? activity,
        string commandType,
        Guid requestId)
    {
        ApplicationDiagnostics.DuplicateCommandsDetected.Add(1,
            new KeyValuePair<string, object?>("command_type", commandType),
            new KeyValuePair<string, object?>("request_id", requestId.ToString()));

        activity?.AddEvent(new ActivityEvent("DuplicateCommandDetected",
            tags: new ActivityTagsCollection
            {
                { "command_type", commandType },
                { "request_id", requestId.ToString() }
            }));
    }

    /// <summary>
    /// Records a duplicate event detection.
    /// </summary>
    /// <param name="activity">The activity to update</param>
    /// <param name="eventId">The event identifier</param>
    /// <param name="handlerName">The handler name</param>
    public static void RecordDuplicateEvent(
        Activity? activity,
        Guid eventId,
        string handlerName)
    {
        ApplicationDiagnostics.DuplicateEventsDetected.Add(1,
            new KeyValuePair<string, object?>("event_id", eventId.ToString()),
            new KeyValuePair<string, object?>("handler_name", handlerName));

        activity?.AddEvent(new ActivityEvent("DuplicateEventDetected",
            tags: new ActivityTagsCollection
            {
                { "event_id", eventId.ToString() },
                { "handler_name", handlerName }
            }));
    }

    /// <summary>
    /// Starts an activity for marking an event as processed.
    /// </summary>
    /// <param name="eventId">The event identifier</param>
    /// <param name="handlerName">The handler name</param>
    /// <returns>The created activity</returns>
    public static Activity? StartMarkEventAsProcessed(Guid eventId, string handlerName)
    {
        var activity = ApplicationDiagnostics.ActivitySource.StartActivity(
            name: "Mark Event as Processed",
            kind: ActivityKind.Internal);

        activity?.SetTag("idempotency.event_id", eventId);
        activity?.SetTag("idempotency.handler_name", handlerName);

        return activity;
    }

    /// <summary>
    /// Records that an event was successfully marked as processed.
    /// </summary>
    /// <param name="activity">The activity to update</param>
    public static void RecordEventMarked(Activity? activity)
    {
        activity?.SetTag("idempotency.marked", true);
    }
}
