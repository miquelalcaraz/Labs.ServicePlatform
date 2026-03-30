using MediatR;

using Microsoft.EntityFrameworkCore;

using Qlt.Platform.Persistence;
using Qlt.Platform.Persistence.Extensions;
using Qlt.Services.Notifications.Models;

namespace Qlt.Services.Notifications.Infrastructure;

public class NotificationsContext : QltDBContext
{
    public DbSet<EmailNotification> EmailNotifications { get; set; } = null!;

    public NotificationsContext(DbContextOptions<NotificationsContext> options, IMediator mediator, QltDbContextConfiguration configuration)
            : base(options, mediator, configuration)
    {
        System.Diagnostics.Debug.WriteLine("NotificationsContext::ctor ->" + this.GetHashCode());
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

    }


}

