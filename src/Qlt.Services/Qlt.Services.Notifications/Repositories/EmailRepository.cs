using Microsoft.EntityFrameworkCore;

using Qlt.Services.Notifications.Infrastructure;
using Qlt.Services.Notifications.Models;

namespace Qlt.Services.Notifications.Repositories;

public class EmailRepository : IEmailRepository
{
    private readonly NotificationsContext _context;

    public EmailRepository(NotificationsContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<EmailNotification> CreateAsync(EmailNotification notification)
    {
        _context.EmailNotifications.Add(notification);
        await _context.SaveChangesAsync();
        return notification;
    }

    public async Task<EmailNotification?> GetByIdAsync(Guid id)
    {
        return await _context.EmailNotifications
            .FirstOrDefaultAsync(u => u.Id == id);
    }

}
