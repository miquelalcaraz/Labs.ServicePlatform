using MediatR;

using Microsoft.Extensions.Logging;

using Miqa.Domain.Services;




namespace Miqa.Platform.Apis;


public abstract class ApiServices(
    IMediator mediator,
    IUserHttpContext userHttpContext,
    ILogger<ApiServices> logger)
{
    public IMediator Mediator { get; set; } = mediator;
    public ILogger<ApiServices> Logger { get; } = logger;
    public IUserHttpContext UserHttpContext { get; } = userHttpContext;
}
