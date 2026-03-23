using MediatR;

namespace QltS.Shared.Messaging.Commands;

public interface ICommand : IRequest<Result>
{
}
public interface ICommand<TResponse> : IRequest<Result<TResponse>>
{
}
