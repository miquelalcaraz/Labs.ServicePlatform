using MediatR;

using Microsoft.EntityFrameworkCore;

using QltS.Core.Persistence.EF.Extensions;
using QltS.Core.Persistence.EF.Relational;
using QltS.Notifications.Models;

namespace QltS.Notifications.Infrastructure;

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

