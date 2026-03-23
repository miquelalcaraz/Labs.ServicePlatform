using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace QltS.Shared.Telemetry.Meters
{
    /// <summary>
    /// Helper class for database migration telemetry operations.
    /// Centralizes activity and metrics recording for migration operations.
    /// </summary>
    public static class MigrationDiagnostics
    {
        public const string SourceName = "QltS.Core.Persistence.Migrations";
        public const string Version = "1.0.0";

        /// <summary>
        /// ActivitySource for migration tracing.
        /// </summary>
        public static readonly ActivitySource ActivitySource = new(SourceName, Version);

        /// <summary>
        /// Meter for migration metrics.
        /// </summary>
        public static readonly Meter Meter = new(SourceName, Version);

        /// <summary>
        /// Counts the total number of migrations executed.
        /// Tags: context_name, success (true/false)
        /// </summary>
        public static readonly Counter<long> MigrationsExecuted = Meter.CreateCounter<long>(
            name: "persistence.migrations.executed",
            unit: "{migration}",
            description: "Total number of database migrations executed");

        /// <summary>
        /// Records the duration of migration operations.
        /// Tags: context_name, operation (migration/seeding), success (true/false)
        /// </summary>
        public static readonly Histogram<double> MigrationDuration = Meter.CreateHistogram<double>(
            name: "persistence.migrations.duration",
            unit: "ms",
            description: "Duration of migration operations in milliseconds");
    }
}
