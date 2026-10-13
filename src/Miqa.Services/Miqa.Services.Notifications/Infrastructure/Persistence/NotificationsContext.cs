using Microsoft.EntityFrameworkCore;

using Miqa.Platform.Persistence;
using Miqa.Platform.Persistence.Options;
using Miqa.Services.Notifications.Domain.Models;

namespace Miqa.Services.Notifications.Infrastructure.Persistence;

public class NotificationsContext : MiqaDBContext
{
    public DbSet<EmailNotification> EmailNotifications { get; set; } = null!;

    public NotificationsContext(DbContextOptions<NotificationsContext> options, MiqaDbContextConfiguration configuration)
            : base(options, configuration)
    {
        System.Diagnostics.Debug.WriteLine("NotificationsContext::ctor ->" + this.GetHashCode());
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

    }


}

