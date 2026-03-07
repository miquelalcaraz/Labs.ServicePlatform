using QltSystem.Identity.Models;

namespace QltSystem.Identity.Repositories
{
    public interface IUserRepository
    {
        Task<AdminUser> CreateAsync(AdminUser user);
        Task<AdminUser?> GetByIdAsync(Guid id);
        Task<IEnumerable<AdminUser>> GetByTenantIdAsync(Guid tenantId);
    }
}
