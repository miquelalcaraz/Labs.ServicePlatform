using MediatR;

using Qlt.Domain.Services;
using Qlt.Platform.Apis;


namespace Qlt.Services.Notifications.APIs
{
    public class NotificationsServices(
                    IMediator mediator,
                    IUserHttpContext userHttpContext,
                    ILogger<ApiServices> logger
        ) : ApiServices(mediator, userHttpContext, logger)
    {
    }


}
