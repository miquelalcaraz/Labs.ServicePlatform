using QltSystem.Identity.Models;

namespace QltSystem.Identity.Repositories
{
    public interface IUserRepository
    {
        Task<ApplicationUser> CreateAsync(ApplicationUser user);
        Task<ApplicationUser?> GetByIdAsync(string id);
        Task<IEnumerable<ApplicationUser>> GetByTenantIdAsync(Guid tenantId);
    }
}
