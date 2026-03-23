using QltS.Notifications.Models;

namespace QltS.Notifications.Repositories
{
    public interface IEmailRepository
    {
        Task<EmailNotification> CreateAsync(EmailNotification notification);
        Task<EmailNotification?> GetByIdAsync(Guid id);
    }
}
