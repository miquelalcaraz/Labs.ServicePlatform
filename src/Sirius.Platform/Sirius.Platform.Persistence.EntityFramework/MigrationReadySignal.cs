namespace Sirius.Platform.Persistence;

/// <summary>
/// Signals that all database migrations have completed.
/// Registered as a singleton and shared between the migration hosted service and consumers
/// that depend on a fully migrated schema (e.g., outbox processor).
/// </summary>
public sealed class MigrationReadySignal
{
    private readonly TaskCompletionSource _tcs = new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>
    /// Waits until migrations have completed or the cancellation token is triggered.
    /// </summary>
    public Task WaitAsync(CancellationToken cancellationToken = default)
    {
        return _tcs.Task.WaitAsync(cancellationToken);
    }

    /// <summary>
    /// Signals that all migrations have completed successfully.
    /// </summary>
    public void Signal() => _tcs.TrySetResult();
}
