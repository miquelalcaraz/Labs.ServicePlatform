using Microsoft.EntityFrameworkCore;

using Miqa.Platform.Persistence;
using Miqa.Platform.Persistence.Options;
using Miqa.Services.Notifications.Domain.Models;

namespace Miqa.Services.Notifications.Infrastructure.Persistence
{
    public class NotificationsReadContext : MiqaReadDbContext
    {
        public DbSet<EmailNotification> EmailNotifications { get; set; } = null!;

        public NotificationsReadContext(DbContextOptions<NotificationsReadContext> options, MiqaReadDbContextConfiguration configuration)
            : base(options, configuration)
        {
            System.Diagnostics.Debug.WriteLine("NotificationsReadContext::ctor ->" + this.GetHashCode());
        }
    }
}
