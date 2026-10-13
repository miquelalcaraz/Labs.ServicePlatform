using MediatR;

using Sirius.Domain.Services;
using Sirius.Platform.Apis;


namespace Sirius.Services.Notifications.APIs
{
    public class NotificationsServices(
                    IMediator mediator,
                    IUserHttpContext userHttpContext,
                    ILogger<ApiServices> logger
        ) : ApiServices(mediator, userHttpContext, logger)
    {
    }


}
