using System;
using System.Collections.Generic;
using System.Diagnostics;

using Miqa.Platform.Meters;

namespace Miqa.Platform.Activities;



/// <summary>
/// Helper class for migration telemetry operations.
/// Provides centralized methods for recording migration activities and metrics.
/// </summary>
public static class MigrationHelper
{
    /// <summary>
    /// Starts a migration operation activity.
    /// </summary>
    /// <param name="contextName">The name of the DbContext being migrated</param>
    /// <returns>The created Activity, or null if not enabled</returns>
    public static Activity? StartMigrationOperation(string contextName)
    {
        var activity = MigrationDiagnostics.ActivitySource.StartActivity(
            name: $"Migration operation {contextName}",
            kind: ActivityKind.Internal);

        activity?.SetTag("migration.context", contextName);
        activity?.SetTag("migration.operation", "full_migration");

        return activity;
    }

    /// <summary>
    /// Starts a migration activity (schema migration only).
    /// </summary>
    /// <param name="contextName">The name of the DbContext being migrated</param>
    /// <returns>The created Activity, or null if not enabled</returns>
    public static Activity? StartMigration(string contextName)
    {
        var activity = MigrationDiagnostics.ActivitySource.StartActivity(
            name: $"Migrating {contextName}",
            kind: ActivityKind.Internal);

        activity?.SetTag("migration.context", contextName);
        activity?.SetTag("migration.operation", "schema_migration");

        return activity;
    }

    /// <summary>
    /// Starts a seeding activity.
    /// </summary>
    /// <param name="contextName">The name of the DbContext being seeded</param>
    /// <returns>The created Activity, or null if not enabled</returns>
    public static Activity? StartSeeding(string contextName)
    {
        var activity = MigrationDiagnostics.ActivitySource.StartActivity(
            name: $"Seeding {contextName}",
            kind: ActivityKind.Internal);

        activity?.SetTag("migration.context", contextName);
        activity?.SetTag("migration.operation", "seeding");

        return activity;
    }

    /// <summary>
    /// Records successful migration operation completion.
    /// </summary>
    /// <param name="activity">The activity to update</param>
    /// <param name="contextName">The name of the DbContext</param>
    /// <param name="operation">The operation type (migration/seeding)</param>
    /// <param name="durationMs">Duration in milliseconds</param>
    public static void RecordMigrationSuccess(
        Activity? activity,
        string contextName,
        string operation,
        double durationMs)
    {
        activity?.SetTag("migration.success", true);
        activity?.SetTag("migration.duration_ms", durationMs);
        activity?.SetStatus(ActivityStatusCode.Ok);

        MigrationDiagnostics.MigrationsExecuted.Add(1,
            new KeyValuePair<string, object?>("context_name", contextName),
            new KeyValuePair<string, object?>("success", "true"));

        MigrationDiagnostics.MigrationDuration.Record(durationMs,
            new KeyValuePair<string, object?>("context_name", contextName),
            new KeyValuePair<string, object?>("operation", operation),
            new KeyValuePair<string, object?>("success", "true"));
    }

    /// <summary>
    /// Records migration operation failure.
    /// </summary>
    /// <param name="activity">The activity to update</param>
    /// <param name="contextName">The name of the DbContext</param>
    /// <param name="operation">The operation type (migration/seeding)</param>
    /// <param name="exception">The exception that occurred</param>
    public static void RecordMigrationFailure(
        Activity? activity,
        string contextName,
        string operation,
        Exception exception)
    {
        activity?.SetTag("migration.success", false);
        activity?.SetTag("error.type", exception.GetType().FullName);
        activity?.SetStatus(ActivityStatusCode.Error, exception.Message);

        MigrationDiagnostics.MigrationsExecuted.Add(1,
            new KeyValuePair<string, object?>("context_name", contextName),
            new KeyValuePair<string, object?>("success", "false"));

        MigrationDiagnostics.MigrationDuration.Record(0,
            new KeyValuePair<string, object?>("context_name", contextName),
            new KeyValuePair<string, object?>("operation", operation),
            new KeyValuePair<string, object?>("success", "false"));
    }
}
