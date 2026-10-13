namespace Miqa.Platform.Messaging.Models;

/// <summary>
/// Represents the publishing status of an outbox message.
/// </summary>
public enum OutboxMessageStatus
{
    NotPublished = 0,
    InProgress = 1,
    Published = 2,
    FailedToPublish = 3
}
