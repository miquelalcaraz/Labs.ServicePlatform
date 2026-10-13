using MediatR;

using Sirius.Domain.Services;
using Sirius.Platform.Apis;


namespace Sirius.Services.Accounts.APIs
{
    public class AccountsServices(
                    IMediator mediator,
                    ICurrentUserContext userHttpContext,
                    ILogger<ApiServices> logger
        ) : ApiServices(mediator, userHttpContext, logger)
    {
    }


}
