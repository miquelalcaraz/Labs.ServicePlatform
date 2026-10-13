using MediatR;

using Sirius.Domain.Services;
using Sirius.Platform.Apis;


namespace Sirius.Services.Notifications.APIs
{
    public class NotificationsServices(
                    IMediator mediator,
                    ICurrentUserContext currentUser,
                    ILogger<NotificationsServices> logger
        ) : ApiServices<NotificationsServices>(mediator, currentUser, logger)
    {
    }


}
