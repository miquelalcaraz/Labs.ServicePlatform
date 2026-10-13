using Microsoft.EntityFrameworkCore;

using Sirius.Platform.Persistence;
using Sirius.Platform.Persistence.Options;
using Sirius.Services.Notifications.Domain.Models;

namespace Sirius.Services.Notifications.Infrastructure.Persistence
{
    public class NotificationsReadContext : SiriusReadDbContext
    {
        public DbSet<EmailNotification> EmailNotifications { get; set; } = null!;

        public NotificationsReadContext(DbContextOptions<NotificationsReadContext> options, SiriusReadDbContextConfiguration configuration)
            : base(options, configuration)
        {
            System.Diagnostics.Debug.WriteLine("NotificationsReadContext::ctor ->" + this.GetHashCode());
        }
    }
}
