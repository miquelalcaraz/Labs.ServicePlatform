using System.Diagnostics.Metrics;

using Qlt.Platform.Meters;

namespace Qlt.Services.Accounts.Application;

/// <summary>
/// Domain-level metrics for the customer activation process lifecycle.
/// Counters are registered on the shared ApplicationDiagnostics meter so they
/// are collected by the existing OpenTelemetry pipeline without additional configuration.
/// </summary>
public static class CustomerActivationDiagnostics
{
    /// <summary>
    /// Total number of customer activation processes started.
    /// Tags: subscription_plan
    /// </summary>
    public static readonly Counter<long> ProcessesStarted = ApplicationDiagnostics.Meter.CreateCounter<long>(
        name: "accounts.activation.processes.started",
        unit: "{process}",
        description: "Total number of customer activation processes started");

    /// <summary>
    /// Total number of customer activation processes successfully completed (GoLive).
    /// </summary>
    public static readonly Counter<long> ProcessesCompleted = ApplicationDiagnostics.Meter.CreateCounter<long>(
        name: "accounts.activation.processes.completed",
        unit: "{process}",
        description: "Total number of customer activation processes successfully completed");

    /// <summary>
    /// Total number of customer activation processes that reached a failed state.
    /// Tags: error_code
    /// </summary>
    public static readonly Counter<long> ProcessesFailed = ApplicationDiagnostics.Meter.CreateCounter<long>(
        name: "accounts.activation.processes.failed",
        unit: "{process}",
        description: "Total number of customer activation processes that reached a failed state");
}
