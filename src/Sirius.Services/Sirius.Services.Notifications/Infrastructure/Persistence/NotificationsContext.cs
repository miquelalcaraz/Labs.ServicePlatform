using Microsoft.EntityFrameworkCore;

using Sirius.Platform.Persistence;
using Sirius.Platform.Persistence.Options;
using Sirius.Services.Notifications.Domain.Models;

namespace Sirius.Services.Notifications.Infrastructure.Persistence;

public class NotificationsContext : SiriusDBContext
{
    public DbSet<EmailNotification> EmailNotifications { get; set; } = null!;

    public NotificationsContext(DbContextOptions<NotificationsContext> options, SiriusDbContextConfiguration configuration)
            : base(options, configuration)
    {
        System.Diagnostics.Debug.WriteLine("NotificationsContext::ctor ->" + this.GetHashCode());
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

    }


}

