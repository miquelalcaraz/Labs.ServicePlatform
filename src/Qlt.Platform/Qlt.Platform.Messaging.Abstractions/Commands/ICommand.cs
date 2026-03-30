using MediatR;

namespace Qlt.Platform.Messaging.Commands;

public interface ICommand : IRequest<Result>
{
}
public interface ICommand<TResponse> : IRequest<Result<TResponse>>
{
}
