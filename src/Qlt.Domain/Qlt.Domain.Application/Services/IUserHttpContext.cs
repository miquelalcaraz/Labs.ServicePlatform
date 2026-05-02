namespace Qlt.Domain.Services;

public interface IUserHttpContext
{
    string GetUserIdentity();

    string GetUserName();

    string? TraceId { get; }

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

