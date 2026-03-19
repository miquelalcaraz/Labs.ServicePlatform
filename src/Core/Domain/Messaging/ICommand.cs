using MediatR;

using QltS.Core.Domain.SeedWork;

namespace QltS.Core.Domain.Messaging;

public interface ICommand : IRequest<Result>
{
}
public interface ICommand<TResponse> : IRequest<Result<TResponse>>
{
}
