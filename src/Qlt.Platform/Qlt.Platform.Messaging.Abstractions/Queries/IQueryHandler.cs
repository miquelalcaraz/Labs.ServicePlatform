using MediatR;

namespace Qlt.Platform.Messaging.Queries;

/// <summary>
/// Handler for queries returning Result&lt;TResponse&gt;. Does not participate in transactions.
/// Inject the read-only DbContext (e.g. AccountReadContext) rather than the write context.
/// </summary>
public interface IQueryHandler<TQuery, TResponse> : IRequestHandler<TQuery, Result<TResponse>>
    where TQuery : IQuery<TResponse> { }
