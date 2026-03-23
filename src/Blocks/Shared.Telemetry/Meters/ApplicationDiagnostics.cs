using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace QltS.Shared.Telemetry.Meters;

/// <summary>
/// Central diagnostics configuration for Core.Application telemetry.
/// Provides ActivitySource for distributed tracing and Meter for custom metrics.
/// </summary>
public static class ApplicationDiagnostics
{
    /// <summary>
    /// Application name used as source name for tracing and metrics.
    /// </summary>
    public const string SourceName = "QltS.Core.Application";

    /// <summary>
    /// Version of the application (used for telemetry versioning).
    /// </summary>
    public const string Version = "1.0.0";

    /// <summary>
    /// ActivitySource for creating custom spans in distributed traces.
    /// </summary>
    public static readonly ActivitySource ActivitySource = new(SourceName, Version);

    /// <summary>
    /// Meter for creating custom metrics (counters, histograms, gauges).
    /// </summary>
    public static readonly Meter Meter = new(SourceName, Version);

    // === Counters ===

    /// <summary>
    /// Counts the total number of commands/queries processed.
    /// Tags: command_type, success (true/false)
    /// </summary>
    public static readonly Counter<long> CommandsProcessed = Meter.CreateCounter<long>(
        name: "application.commands.processed",
        unit: "{command}",
        description: "Total number of commands/queries processed");

    /// <summary>
    /// Counts the total number of command handlers processed.
    /// Tags: command_type, handler_type, success (true/false)
    /// </summary>
    public static readonly Counter<long> CommandHandlersProcessed = Meter.CreateCounter<long>(
        name: "application.command_handlers.processed",
        unit: "{handler}",
        description: "Total number of command handlers processed");

    /// <summary>
    /// Counts the total number of validation failures.
    /// Tags: command_type, error_count
    /// </summary>
    public static readonly Counter<long> ValidationFailures = Meter.CreateCounter<long>(
        name: "application.validation.failures",
        unit: "{failure}",
        description: "Total number of validation failures");

    /// <summary>
    /// Counts the total number of domain events published.
    /// Tags: event_type
    /// </summary>
    public static readonly Counter<long> DomainEventsPublished = Meter.CreateCounter<long>(
        name: "application.domain_events.published",
        unit: "{event}",
        description: "Total number of domain events published");

    /// <summary>
    /// Counts the total number of domain event handlers processed.
    /// Tags: event_type, handler_type, success (true/false)
    /// </summary>
    public static readonly Counter<long> DomainEventHandlersProcessed = Meter.CreateCounter<long>(
        name: "application.domain_event_handlers.processed",
        unit: "{handler}",
        description: "Total number of domain event handlers processed");

    /// <summary>
    /// Counts the total number of integration events published.
    /// Tags: event_type, success (true/false)
    /// </summary>
    public static readonly Counter<long> IntegrationEventsPublished = Meter.CreateCounter<long>(
        name: "application.integration_events.published",
        unit: "{event}",
        description: "Total number of integration events published");

    /// <summary>
    /// Counts the total number of integration events received.
    /// Tags: event_type, success (true/false)
    /// </summary>
    public static readonly Counter<long> IntegrationEventsReceived = Meter.CreateCounter<long>(
        name: "application.integration_events.received",
        unit: "{event}",
        description: "Total number of integration events received");

    /// <summary>
    /// Counts the total number of integration event handlers processed.
    /// Tags: event_type, handler_type, success (true/false)
    /// </summary>
    public static readonly Counter<long> IntegrationEventHandlersProcessed = Meter.CreateCounter<long>(
        name: "application.integration_event_handlers.processed",
        unit: "{handler}",
        description: "Total number of integration event handlers processed");

    /// <summary>
    /// Counts the total number of duplicate events detected by idempotency check.
    /// Tags: event_type, handler_name
    /// </summary>
    public static readonly Counter<long> DuplicateEventsDetected = Meter.CreateCounter<long>(
        name: "application.idempotency.duplicates_detected",
        unit: "{event}",
        description: "Total number of duplicate events detected");

    /// <summary>
    /// Counts the total number of duplicate commands detected by idempotency check.
    /// Tags: command_type, request_id
    /// </summary>
    public static readonly Counter<long> DuplicateCommandsDetected = Meter.CreateCounter<long>(
        name: "application.idempotency.duplicate_commands",
        unit: "{command}",
        description: "Total number of duplicate commands detected");

    /// <summary>
    /// Counts the total number of events saved to the outbox.
    /// Tags: event_type
    /// </summary>
    public static readonly Counter<long> OutboxEventsSaved = Meter.CreateCounter<long>(
        name: "application.outbox.events_saved",
        unit: "{event}",
        description: "Total number of events saved to the transactional outbox");

    /// <summary>
    /// Counts outbox event state transitions.
    /// Tags: status (Published, InProgress, PublishedFailed)
    /// </summary>
    public static readonly Counter<long> OutboxEventStateChanges = Meter.CreateCounter<long>(
        name: "application.outbox.state_changes",
        unit: "{change}",
        description: "Total number of outbox event state changes");

    /// <summary>
    /// Counts failed outbox events (reached PublishedFailed state).
    /// Tags: event_type
    /// </summary>
    public static readonly Counter<long> OutboxEventsFailed = Meter.CreateCounter<long>(
        name: "application.outbox.events_failed",
        unit: "{event}",
        description: "Total number of outbox events that failed to publish");

    // === Histograms ===

    /// <summary>
    /// Records the duration of command/query execution.
    /// Tags: command_type, success (true/false)
    /// </summary>
    public static readonly Histogram<double> CommandDuration = Meter.CreateHistogram<double>(
        name: "application.commands.duration",
        unit: "ms",
        description: "Duration of command/query execution in milliseconds");

    /// <summary>
    /// Records the duration of command handler execution.
    /// Tags: command_type, handler_type
    /// </summary>
    public static readonly Histogram<double> CommandHandlerDuration = Meter.CreateHistogram<double>(
        name: "application.command_handlers.duration",
        unit: "ms",
        description: "Duration of command handler execution in milliseconds");

    /// <summary>
    /// Records the duration of validation.
    /// Tags: command_type, validator_count
    /// </summary>
    public static readonly Histogram<double> ValidationDuration = Meter.CreateHistogram<double>(
        name: "application.validation.duration",
        unit: "ms",
        description: "Duration of validation in milliseconds");

    /// <summary>
    /// Records the duration of database transactions.
    /// Tags: command_type, success (true/false)
    /// </summary>
    public static readonly Histogram<double> TransactionDuration = Meter.CreateHistogram<double>(
        name: "application.transactions.duration",
        unit: "ms",
        description: "Duration of database transactions in milliseconds");

    /// <summary>
    /// Records the duration of integration event handler execution.
    /// Tags: event_type, handler_type
    /// </summary>
    public static readonly Histogram<double> IntegrationEventHandlerDuration = Meter.CreateHistogram<double>(
        name: "application.integration_event_handlers.duration",
        unit: "ms",
        description: "Duration of integration event handler execution in milliseconds");

    /// <summary>
    /// Records the duration of domain event handler execution.
    /// Tags: event_type, handler_type
    /// </summary>
    public static readonly Histogram<double> DomainEventHandlerDuration = Meter.CreateHistogram<double>(
        name: "application.domain_event_handlers.duration",
        unit: "ms",
        description: "Duration of domain event handler execution in milliseconds");


    /// <summary>
    /// Records the number of validation errors per request.
    /// Tags: command_type
    /// </summary>
    public static readonly Histogram<int> ValidationErrorCount = Meter.CreateHistogram<int>(
        name: "application.validation.error_count",
        unit: "{error}",
        description: "Number of validation errors per request");

    /// <summary>
    /// Records the duration of idempotency checks.
    /// Tags: check_type (event/command), is_duplicate (true/false)
    /// </summary>
    public static readonly Histogram<double> IdempotencyCheckDuration = Meter.CreateHistogram<double>(
        name: "application.idempotency.check_duration",
        unit: "ms",
        description: "Duration of idempotency check in milliseconds");

    /// <summary>
    /// Records the duration of outbox operations.
    /// Tags: operation (retrieve/save/update_status)
    /// </summary>
    public static readonly Histogram<double> OutboxOperationDuration = Meter.CreateHistogram<double>(
        name: "application.outbox.operation_duration",
        unit: "ms",
        description: "Duration of outbox operations in milliseconds");

    /// <summary>
    /// Records the number of pending events in each outbox batch.
    /// </summary>
    public static readonly Histogram<int> OutboxBatchSize = Meter.CreateHistogram<int>(
        name: "application.outbox.batch_size",
        unit: "{event}",
        description: "Number of events in outbox publishing batch");
}
