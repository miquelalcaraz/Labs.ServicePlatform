using Sirius.Platform.Persistence.Repositories;
using Sirius.Services.Notifications.Domain.Models;
using Sirius.Services.Notifications.Domain.Repositories;

namespace Sirius.Services.Notifications.Infrastructure.Persistence.Repositories;

public class EmailNotificationRepository : Repository<EmailNotification>, IEmailNotificationRepository
{
    public EmailNotificationRepository(NotificationsContext context) : base(context)
    {
    }
}
