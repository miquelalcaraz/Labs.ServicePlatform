using MediatR;

using Microsoft.Extensions.Logging;

using QltS.Core.Application.Services;




namespace QltS.Core.API.Apis;


public abstract class ApiServices(
    IMediator mediator,
    IUserHttpContext identityService,
    ILogger<ApiServices> logger)
{
    public IMediator Mediator { get; set; } = mediator;
    public ILogger<ApiServices> Logger { get; } = logger;
    public IUserHttpContext IdentityService { get; } = identityService;
}
