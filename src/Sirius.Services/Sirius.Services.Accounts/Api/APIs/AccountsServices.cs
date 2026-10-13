using MediatR;

using Sirius.Domain.Services;
using Sirius.Platform.Apis;


namespace Sirius.Services.Accounts.APIs
{
    public class AccountsServices(
                    IMediator mediator,
                    ICurrentUserContext userHttpContext,
                    ILogger<AccountsServices> logger
        ) : ApiServices<AccountsServices>(mediator, userHttpContext, logger)
    {
    }


}
