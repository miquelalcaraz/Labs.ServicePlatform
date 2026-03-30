namespace Qlt.Platform.Messaging.Idempotency;

/// <summary>
/// Identifies the type of operation being tracked for idempotency.
/// </summary>
public enum IdempotencyScope
{
    Request,
    Event
}
