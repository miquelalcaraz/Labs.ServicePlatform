using Qlt.Domain.SeedWork;
using Qlt.Services.Accounts.Domain.Models.TenantAggregate;

namespace Qlt.Services.Accounts.Domain.Repositories;

public interface ITenantRepository : IRepository<Tenant>
{
    Tenant Add(Tenant tenant);

    void Update(Tenant tenant);

    Task<Tenant?> GetAsync(Guid tenantId);
}
