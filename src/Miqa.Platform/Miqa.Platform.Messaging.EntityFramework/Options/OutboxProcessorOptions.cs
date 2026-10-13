using System;

namespace Miqa.Platform.Messaging.Options;

/// <summary>
/// Configuration options for the background outbox processor.
/// </summary>
public class OutboxProcessorOptions
{
    /// <summary>
    /// Interval between polling cycles. Default: 30 seconds.
    /// </summary>
    public TimeSpan PollingInterval { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Maximum number of events to process per polling cycle. Default: 50.
    /// </summary>
    public int BatchSize { get; set; } = 50;

    /// <summary>
    /// Maximum number of publish attempts before an event is abandoned. Default: 5.
    /// </summary>
    public int MaxRetryCount { get; set; } = 5;
}
