namespace QltSystem.Core.Persistence.EF.Idempotency;

public enum EventStateEnum
{
    NotPublished = 0,
    InProgress = 1,
    Published = 2,
    PublishedFailed = 3
}

