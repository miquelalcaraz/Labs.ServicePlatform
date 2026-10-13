using MediatR;

using Sirius.Domain.Services;
using Sirius.Platform.Apis;


namespace Sirius.Services.Notifications.APIs
{
    public class NotificationsServices(
                    IMediator mediator,
                    ICurrentUserContext userHttpContext,
                    ILogger<NotificationsServices> logger
        ) : ApiServices<NotificationsServices>(mediator, userHttpContext, logger)
    {
    }


}
