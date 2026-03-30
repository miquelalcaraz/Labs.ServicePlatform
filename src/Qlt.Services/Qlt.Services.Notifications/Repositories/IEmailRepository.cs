using Qlt.Services.Notifications.Models;

namespace Qlt.Services.Notifications.Repositories
{
    public interface IEmailRepository
    {
        Task<EmailNotification> CreateAsync(EmailNotification notification);
        Task<EmailNotification?> GetByIdAsync(Guid id);
    }
}
