using MediatR;

using Microsoft.Extensions.Logging;

using Sirius.Domain.Services;




namespace Sirius.Platform.Apis;


public abstract class ApiServices<TSelf>(
    IMediator mediator,
    ICurrentUserContext currentUser,
    ILogger<TSelf> logger)
{
    public IMediator Mediator { get; set; } = mediator;
    public ILogger<TSelf> Logger { get; } = logger;
    public ICurrentUserContext CurrentUser { get; } = currentUser;
}
