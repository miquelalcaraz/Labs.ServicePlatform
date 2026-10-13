using Sirius.Platform.Persistence.Repositories;
using Sirius.Services.Accounts.Domain.Models.TenantAggregate;
using Sirius.Services.Accounts.Domain.Repositories;

namespace Sirius.Services.Accounts.Infrastructure.Persistence.Repositories;

public class TenantRepository : Repository<Tenant>, ITenantRepository
{
    public TenantRepository(AccountContext context) : base(context)
    {
    }
}
