using MediatR;

using Qlt.Domain.Services;
using Qlt.Platform.Apis;


namespace Qlt.Services.Accounts.APIs
{
    public class AccountsServices(
                    IMediator mediator,
                    IUserHttpContext userHttpContext,
                    ILogger<ApiServices> logger
        ) : ApiServices(mediator, userHttpContext, logger)
    {
    }


}
