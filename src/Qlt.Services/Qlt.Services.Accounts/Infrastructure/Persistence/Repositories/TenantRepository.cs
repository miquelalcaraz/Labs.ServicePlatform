using Qlt.Platform.Persistence.Repositories;
using Qlt.Services.Accounts.Domain.Models.TenantAggregate;
using Qlt.Services.Accounts.Domain.Repositories;

namespace Qlt.Services.Accounts.Infrastructure.Persistence.Repositories;

public class TenantRepository : Repository<Tenant>, ITenantRepository
{
    public TenantRepository(AccountContext context) : base(context)
    {
    }
}
