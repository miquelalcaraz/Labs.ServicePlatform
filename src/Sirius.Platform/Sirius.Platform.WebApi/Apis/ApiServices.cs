using MediatR;

using Microsoft.Extensions.Logging;

using Sirius.Domain.Services;




namespace Sirius.Platform.Apis;


public abstract class ApiServices(
    IMediator mediator,
    ICurrentUserContext userHttpContext,
    ILogger<ApiServices> logger)
{
    public IMediator Mediator { get; set; } = mediator;
    public ILogger<ApiServices> Logger { get; } = logger;
    public ICurrentUserContext CurrentUser { get; } = userHttpContext;
}
