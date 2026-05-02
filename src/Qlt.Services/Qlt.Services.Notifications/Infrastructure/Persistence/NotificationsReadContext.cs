using Microsoft.EntityFrameworkCore;

using Qlt.Platform.Persistence;
using Qlt.Platform.Persistence.Options;
using Qlt.Services.Notifications.Domain.Models;

namespace Qlt.Services.Notifications.Infrastructure.Persistence
{
    public class NotificationsReadContext : QltReadDbContext
    {
        public DbSet<EmailNotification> EmailNotifications { get; set; } = null!;

        public NotificationsReadContext(DbContextOptions<NotificationsReadContext> options, QltReadDbContextConfiguration configuration)
            : base(options, configuration)
        {
            System.Diagnostics.Debug.WriteLine("NotificationsReadContext::ctor ->" + this.GetHashCode());
        }
    }
}
