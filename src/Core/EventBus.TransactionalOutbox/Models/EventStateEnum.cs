namespace QltS.Core.EventBus.TransactionalOutbox.Models;

/// <summary>
/// Represents the state of an integration event in the outbox.
/// </summary>
public enum EventStateEnum
{
    NotPublished = 0,
    InProgress = 1,
    Published = 2,
    PublishedFailed = 3
}
