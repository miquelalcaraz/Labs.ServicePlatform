using MediatR;

using Miqa.Domain.Services;
using Miqa.Platform.Apis;


namespace Miqa.Services.Accounts.APIs
{
    public class AccountsServices(
                    IMediator mediator,
                    IUserHttpContext userHttpContext,
                    ILogger<ApiServices> logger
        ) : ApiServices(mediator, userHttpContext, logger)
    {
    }


}
