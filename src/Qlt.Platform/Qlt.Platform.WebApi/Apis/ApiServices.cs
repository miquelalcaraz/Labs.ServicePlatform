using MediatR;

using Microsoft.Extensions.Logging;

using Qlt.Domain.Services;




namespace Qlt.Platform.Apis;


public abstract class ApiServices(
    IMediator mediator,
    IUserHttpContext userHttpContext,
    ILogger<ApiServices> logger)
{
    public IMediator Mediator { get; set; } = mediator;
    public ILogger<ApiServices> Logger { get; } = logger;
    public IUserHttpContext UserHttpContext { get; } = userHttpContext;
}
