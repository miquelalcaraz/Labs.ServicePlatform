namespace Miqa.Platform.Messaging.Models;

/// <summary>
/// Identifies the type of operation being tracked for idempotency.
/// </summary>
public enum IdempotencyScope
{
    Request,
    Event
}
