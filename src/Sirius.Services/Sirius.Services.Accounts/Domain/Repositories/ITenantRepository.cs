using Sirius.Domain.SeedWork;
using Sirius.Services.Accounts.Domain.Models.TenantAggregate;

namespace Sirius.Services.Accounts.Domain.Repositories;

public interface ITenantRepository : IRepository<Tenant>
{
    Tenant Add(Tenant tenant);

    void Update(Tenant tenant);

    Task<Tenant?> GetAsync(Guid tenantId);
}
