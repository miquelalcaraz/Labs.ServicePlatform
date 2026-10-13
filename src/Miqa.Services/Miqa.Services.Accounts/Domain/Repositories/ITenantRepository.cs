using Miqa.Domain.SeedWork;
using Miqa.Services.Accounts.Domain.Models.TenantAggregate;

namespace Miqa.Services.Accounts.Domain.Repositories;

public interface ITenantRepository : IRepository<Tenant>
{
    Tenant Add(Tenant tenant);

    void Update(Tenant tenant);

    Task<Tenant?> GetAsync(Guid tenantId);
}
