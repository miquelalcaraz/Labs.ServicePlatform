using Qlt.Platform.Meters;

using System;
using System.Diagnostics;

namespace Qlt.Platform.Activities;

/// <summary>
/// Helper class for creating and managing OpenTelemetry activities (spans) in handlers.
/// Centralizes common activity operations to reduce code duplication across base classes.
/// </summary>
public static class ActivityHelper
{
    /// <summary>
    /// Starts a new activity for handler execution.
    /// </summary>
    /// <param name="activityName">The name of the activity (e.g., "Execute CreateOrganizationCommand")</param>
    /// <param name="kind">The kind of activity (default: Internal)</param>
    /// <returns>The created activity, or null if creation failed</returns>
    public static Activity? StartHandlerActivity(string activityName, ActivityKind kind = ActivityKind.Internal)
    {
        return ApplicationDiagnostics.ActivitySource.StartActivity(
            name: activityName,
            kind: kind);
    }

    /// <summary>
    /// Sets common tags for a handler activity.
    /// </summary>
    /// <param name="activity">The activity to tag</param>
    /// <param name="messageType">The type name of the message being handled (command/event)</param>
    /// <param name="handlerType">The type name of the handler</param>
    /// <param name="category">The category of handler (command, domain_event, integration_event)</param>
    public static void SetHandlerTags(
        Activity? activity,
        string messageType,
        string handlerType,
        string category)
    {
        if (activity == null) return;

        activity.SetTag($"{category}.type", messageType);
        activity.SetTag("handler.type", handlerType);
        activity.SetTag("handler.category", category);
    }

    /// <summary>
    /// Records the success or failure of a handler execution.
    /// </summary>
    /// <param name="activity">The activity to update</param>
    /// <param name="durationMs">The execution duration in milliseconds</param>
    /// <param name="success">Whether the execution was successful</param>
    public static void RecordHandlerSuccess(
        Activity? activity,
        double durationMs,
        bool success)
    {
        if (activity == null) return;

        activity.SetTag("handler.success", success);
        activity.SetTag("handler.duration_ms", durationMs);
    }

    /// <summary>
    /// Records error information from a Result failure.
    /// </summary>
    /// <param name="activity">The activity to update</param>
    /// <param name="errorCode">The error code from Result</param>
    /// <param name="errorMessage">The error message from Result</param>
    public static void RecordResultError(
        Activity? activity,
        string errorCode,
        string errorMessage)
    {
        if (activity == null) return;

        activity.SetTag("error.code", errorCode);
        activity.SetTag("error.message", errorMessage);
    }

    /// <summary>
    /// Records exception information and sets activity status to error.
    /// </summary>
    /// <param name="activity">The activity to update</param>
    /// <param name="exception">The exception that occurred</param>
    public static void RecordException(Activity? activity, Exception exception)
    {
        if (activity == null) return;

        activity.SetTag("error.type", exception.GetType().FullName);
        activity.SetStatus(ActivityStatusCode.Error, exception.Message);
    }

    /// <summary>
    /// Sets event-specific tags for integration event handlers.
    /// </summary>
    /// <param name="activity">The activity to update</param>
    /// <param name="eventId">The event identifier</param>
    /// <param name="eventCreationDate">The event creation timestamp</param>
    public static void SetEventTags(
        Activity? activity,
        Guid eventId,
        DateTime eventCreationDate)
    {
        if (activity == null) return;

        activity.SetTag("event.id", eventId.ToString());
        activity.SetTag("event.creation_date", eventCreationDate.ToString("O"));
    }

    /// <summary>
    /// Sets domain event flag tag.
    /// </summary>
    /// <param name="activity">The activity to update</param>
    public static void SetDomainEventTag(Activity? activity)
    {
        activity?.SetTag("event.domain", true);
    }
}
