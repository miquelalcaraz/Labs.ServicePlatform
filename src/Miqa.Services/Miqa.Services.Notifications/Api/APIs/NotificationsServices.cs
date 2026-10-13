using MediatR;

using Miqa.Domain.Services;
using Miqa.Platform.Apis;


namespace Miqa.Services.Notifications.APIs
{
    public class NotificationsServices(
                    IMediator mediator,
                    IUserHttpContext userHttpContext,
                    ILogger<ApiServices> logger
        ) : ApiServices(mediator, userHttpContext, logger)
    {
    }


}
