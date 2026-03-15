using MediatR;

using Microsoft.Extensions.Logging;

using QltSystem.Core.API.Services;


namespace QltSystem.Core.API.Apis;


public abstract class ApiServices(
    IMediator mediator,
    IIdentityService identityService,
    ILogger<ApiServices> logger)
{
    public IMediator Mediator { get; set; } = mediator;
    public ILogger<ApiServices> Logger { get; } = logger;
    public IIdentityService IdentityService { get; } = identityService;
}
