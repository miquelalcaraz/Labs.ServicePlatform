namespace QltS.Core.Application.Services;

public interface IUserHttpContext
{
    string GetUserIdentity();

    string GetUserName();

    /// <summary>
    /// Gets the unique identifier for the current request.
    /// Used for idempotency and request tracking.
    /// </summary>
    string? RequestId { get; }

    /// <summary>
    /// Gets the correlation identifier that spans multiple requests/operations.
    /// Used for distributed tracing across services.
    /// </summary>
    string? CorrelationId { get; }
}

