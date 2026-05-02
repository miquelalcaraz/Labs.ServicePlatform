using Microsoft.EntityFrameworkCore;

using Qlt.Platform.Persistence;
using Qlt.Platform.Persistence.Options;
using Qlt.Services.Notifications.Domain.Models;

namespace Qlt.Services.Notifications.Infrastructure.Persistence;

public class NotificationsContext : QltDBContext
{
    public DbSet<EmailNotification> EmailNotifications { get; set; } = null!;

    public NotificationsContext(DbContextOptions<NotificationsContext> options, QltDbContextConfiguration configuration)
            : base(options, configuration)
    {
        System.Diagnostics.Debug.WriteLine("NotificationsContext::ctor ->" + this.GetHashCode());
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

    }


}

