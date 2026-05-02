using Qlt.Platform.Persistence.Repositories;
using Qlt.Services.Notifications.Domain.Models;
using Qlt.Services.Notifications.Domain.Repositories;

namespace Qlt.Services.Notifications.Infrastructure.Persistence.Repositories;

public class EmailNotificationRepository : Repository<EmailNotification>, IEmailNotificationRepository
{
    public EmailNotificationRepository(NotificationsContext context) : base(context)
    {
    }
}
