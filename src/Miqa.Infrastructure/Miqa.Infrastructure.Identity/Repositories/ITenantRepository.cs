using Miqa.Infrastructure.Identity.Models;

namespace Miqa.Infrastructure.Identity.Repositories
{
    public interface ITenantRepository
    {
        Task<Tenant> CreateAsync(Tenant tenant);
        Task<Tenant?> GetByIdAsync(Guid id);
        Task<Tenant?> GetByCompanyIdAsync(Guid companyId);
    }
}
