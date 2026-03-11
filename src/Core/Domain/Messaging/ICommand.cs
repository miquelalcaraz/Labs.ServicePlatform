using MediatR;

using QltSystem.Core.Domain.SeedWork;

namespace QltSystem.Core.Domain.Messaging;

public interface ICommand : IRequest<Result>
{
}
public interface ICommand<TResponse> : IRequest<Result<TResponse>>
{
}
