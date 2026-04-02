using MediatR;

using Qlt.Domain.Services;
using Qlt.Platform.Apis;


namespace Qlt.Services.Accounts.Api
{
    public class AccountsServices(
                    IMediator mediator,
                    IUserHttpContext identityService,
                    ILogger<ApiServices> logger
        ) : ApiServices(mediator, identityService, logger)
    {
    }


}
