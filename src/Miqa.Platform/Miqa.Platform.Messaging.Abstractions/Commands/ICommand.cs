using MediatR;

namespace Miqa.Platform.Messaging.Commands;


public interface ICommand : IRequest<Result>, ICommandRequest { }
public interface ICommand<TResponse> : IRequest<Result<TResponse>>, ICommandRequest { }
