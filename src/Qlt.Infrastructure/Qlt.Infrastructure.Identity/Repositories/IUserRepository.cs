using Qlt.Infrastructure.Identity.Models;

namespace Qlt.Infrastructure.Identity.Repositories
{
    public interface IUserRepository
    {
        Task<ApplicationUser> CreateAsync(ApplicationUser user);
        Task<ApplicationUser?> GetByIdAsync(string id);
        Task<ApplicationUser?> GetByEmailAsync(string email);
        Task<IEnumerable<ApplicationUser>> GetByTenantIdAsync(Guid tenantId);
    }
}
