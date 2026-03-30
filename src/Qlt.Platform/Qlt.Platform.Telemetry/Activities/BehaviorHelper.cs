using Qlt.Platform.Meters;

using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace Qlt.Platform.Activities;

/// <summary>
/// Helper class for MediatR pipeline behaviors with built-in telemetry.
/// Centralizes common activity operations for validation, transaction, and logging behaviors.
/// </summary>
public static class BehaviorHelper
{
    /// <summary>
    /// Starts a validation activity.
    /// </summary>
    /// <param name="commandName">The command/request name</param>
    /// <param name="validatorCount">Number of validators being executed</param>
    /// <returns>The created activity</returns>
    public static Activity? StartValidationActivity(string commandName, int validatorCount)
    {
        var activity = ApplicationDiagnostics.ActivitySource.StartActivity(
            name: $"Validation {commandName}",
            kind: ActivityKind.Internal);

        activity?.SetTag("validation.command", commandName);
        activity?.SetTag("validation.validator_count", validatorCount);

        return activity;
    }

    /// <summary>
    /// Records successful validation.
    /// </summary>
    /// <param name="activity">The activity to update</param>
    /// <param name="commandName">The command name</param>
    /// <param name="validatorCount">Number of validators executed</param>
    /// <param name="durationMs">The validation duration in milliseconds</param>
    public static void RecordValidationSuccess(
        Activity? activity,
        string commandName,
        int validatorCount,
        double durationMs)
    {
        ApplicationDiagnostics.ValidationDuration.Record(durationMs,
            new KeyValuePair<string, object?>("command_type", commandName),
            new KeyValuePair<string, object?>("validator_count", validatorCount));

        activity?.SetTag("validation.success", true);
        activity?.SetTag("validation.duration_ms", durationMs);
    }

    /// <summary>
    /// Records validation failure.
    /// </summary>
    /// <param name="activity">The activity to update</param>
    /// <param name="commandName">The command name</param>
    /// <param name="validatorCount">Number of validators executed</param>
    /// <param name="errorCount">Number of validation errors</param>
    /// <param name="durationMs">The validation duration in milliseconds</param>
    /// <param name="errorMessages">Formatted error messages</param>
    public static void RecordValidationFailure(
        Activity? activity,
        string commandName,
        int validatorCount,
        int errorCount,
        double durationMs,
        string errorMessages)
    {
        // Record validation duration
        ApplicationDiagnostics.ValidationDuration.Record(durationMs,
            new KeyValuePair<string, object?>("command_type", commandName),
            new KeyValuePair<string, object?>("validator_count", validatorCount));

        // Record validation failure metrics
        ApplicationDiagnostics.ValidationFailures.Add(1,
            new KeyValuePair<string, object?>("command_type", commandName),
            new KeyValuePair<string, object?>("error_count", errorCount));

        ApplicationDiagnostics.ValidationErrorCount.Record(errorCount,
            new KeyValuePair<string, object?>("command_type", commandName));

        // Update activity
        activity?.SetTag("validation.failed", true);
        activity?.SetTag("validation.error_count", errorCount);
        activity?.SetStatus(ActivityStatusCode.Error, "Validation failed");
        activity?.AddEvent(new ActivityEvent("ValidationFailed",
            tags: new ActivityTagsCollection
            {
                { "errors", errorMessages }
            }));
    }

    /// <summary>
    /// Starts a transaction activity.
    /// </summary>
    /// <param name="commandName">The command name</param>
    /// <returns>The created activity</returns>
    public static Activity? StartTransactionActivity(string commandName)
    {
        var activity = ApplicationDiagnostics.ActivitySource.StartActivity(
            name: $"Transaction {commandName}",
            kind: ActivityKind.Internal);

        activity?.SetTag("transaction.command", commandName);

        return activity;
    }

    /// <summary>
    /// Sets transaction tags for nested transactions.
    /// </summary>
    /// <param name="activity">The activity to update</param>
    public static void SetNestedTransactionTag(Activity? activity)
    {
        activity?.SetTag("transaction.nested", true);
    }

    /// <summary>
    /// Sets the transaction ID tag.
    /// </summary>
    /// <param name="activity">The activity to update</param>
    /// <param name="transactionId">The transaction identifier</param>
    public static void SetTransactionId(Activity? activity, Guid transactionId)
    {
        activity?.SetTag("transaction.id", transactionId);
    }

    /// <summary>
    /// Records successful transaction.
    /// </summary>
    /// <param name="activity">The activity to update</param>
    /// <param name="commandName">The command name</param>
    /// <param name="durationMs">The transaction duration in milliseconds</param>
    public static void RecordTransactionSuccess(
        Activity? activity,
        string commandName,
        double durationMs)
    {
        ApplicationDiagnostics.TransactionDuration.Record(durationMs,
            new KeyValuePair<string, object?>("command_type", commandName),
            new KeyValuePair<string, object?>("success", "true"));

        activity?.SetTag("transaction.success", true);
        activity?.SetTag("transaction.duration_ms", durationMs);
    }

    /// <summary>
    /// Records failed transaction.
    /// </summary>
    /// <param name="activity">The activity to update</param>
    /// <param name="commandName">The command name</param>
    /// <param name="exception">The exception that caused the failure</param>
    public static void RecordTransactionFailure(
        Activity? activity,
        string commandName,
        Exception exception)
    {
        ApplicationDiagnostics.TransactionDuration.Record(0,
            new KeyValuePair<string, object?>("command_type", commandName),
            new KeyValuePair<string, object?>("success", "false"));

        activity?.SetTag("transaction.success", false);
        activity?.SetTag("error.type", exception.GetType().FullName);
        activity?.SetStatus(ActivityStatusCode.Error, exception.Message);
    }

    /// <summary>
    /// Starts a command processing activity (for LoggingBehavior).
    /// </summary>
    /// <param name="commandName">The command name</param>
    /// <returns>The created activity</returns>
    public static Activity? StartCommandActivity(string commandName)
    {
        return ApplicationDiagnostics.ActivitySource.StartActivity(
            name: $"Command {commandName}",
            kind: ActivityKind.Internal);
    }

    /// <summary>
    /// Sets command context tags (request ID, correlation ID, etc.).
    /// </summary>
    /// <param name="activity">The activity to update</param>
    /// <param name="commandName">The command name</param>
    /// <param name="requestId">The request identifier</param>
    /// <param name="correlationId">The correlation identifier</param>
    public static void SetCommandContextTags(
        Activity? activity,
        string commandName,
        string requestId,
        string correlationId)
    {
        activity?.SetTag("command.name", commandName);
        activity?.SetTag("request.id", requestId);
        activity?.SetTag("correlation.id", correlationId);
    }

    /// <summary>
    /// Records command execution metrics.
    /// </summary>
    /// <param name="commandName">The command name</param>
    /// <param name="durationMs">The execution duration in milliseconds</param>
    /// <param name="success">Whether the command succeeded</param>
    public static void RecordCommandExecution(
        string commandName,
        double durationMs,
        bool success)
    {
        var successTag = success.ToString().ToLowerInvariant();

        ApplicationDiagnostics.CommandsProcessed.Add(1,
            new KeyValuePair<string, object?>("command_type", commandName),
            new KeyValuePair<string, object?>("success", successTag));

        ApplicationDiagnostics.CommandDuration.Record(durationMs,
            new KeyValuePair<string, object?>("command_type", commandName),
            new KeyValuePair<string, object?>("success", successTag));
    }

    /// <summary>
    /// Records command success with activity tags.
    /// </summary>
    /// <param name="activity">The activity to update</param>
    /// <param name="durationMs">The execution duration in milliseconds</param>
    /// <param name="isSlow">Whether the command exceeded the slow threshold</param>
    public static void RecordCommandSuccess(
        Activity? activity,
        double durationMs,
        bool isSlow = false)
    {
        activity?.SetTag("command.success", true);
        activity?.SetTag("command.duration_ms", durationMs);

        if (isSlow)
        {
            activity?.SetTag("command.slow", true);
        }
    }

    /// <summary>
    /// Records command failure with activity tags.
    /// </summary>
    /// <param name="activity">The activity to update</param>
    /// <param name="durationMs">The execution duration in milliseconds</param>
    /// <param name="exception">The exception that caused the failure</param>
    public static void RecordCommandFailure(
        Activity? activity,
        double durationMs,
        Exception exception)
    {
        activity?.SetTag("command.success", false);
        activity?.SetTag("command.duration_ms", durationMs);
        activity?.SetTag("error.type", exception.GetType().FullName);
        activity?.SetStatus(ActivityStatusCode.Error, exception.Message);
    }
}
