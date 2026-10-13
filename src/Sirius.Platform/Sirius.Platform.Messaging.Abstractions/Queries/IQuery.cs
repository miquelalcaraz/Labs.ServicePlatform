using MediatR;

namespace Sirius.Platform.Messaging.Queries;

/// <summary>
/// Marker for all queries. Pipeline behaviors use this to skip write-only concerns (transactions, idempotency).
/// </summary>
public interface IQuery<TResponse> : IRequest<Result<TResponse>> { }
