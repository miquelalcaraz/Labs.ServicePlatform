using Miqa.Platform.Persistence.Repositories;
using Miqa.Services.Notifications.Domain.Models;
using Miqa.Services.Notifications.Domain.Repositories;

namespace Miqa.Services.Notifications.Infrastructure.Persistence.Repositories;

public class EmailNotificationRepository : Repository<EmailNotification>, IEmailNotificationRepository
{
    public EmailNotificationRepository(NotificationsContext context) : base(context)
    {
    }
}
