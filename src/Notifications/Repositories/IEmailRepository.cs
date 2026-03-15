using QltSystem.Notifications.Models;

namespace QltSystem.Notifications.Repositories
{
    public interface IEmailRepository
    {
        Task<EmailNotification> CreateAsync(EmailNotification notification);
        Task<EmailNotification?> GetByIdAsync(Guid id);
    }
}
