using Miqa.Platform.Persistence.Repositories;
using Miqa.Services.Accounts.Domain.Models.TenantAggregate;
using Miqa.Services.Accounts.Domain.Repositories;

namespace Miqa.Services.Accounts.Infrastructure.Persistence.Repositories;

public class TenantRepository : Repository<Tenant>, ITenantRepository
{
    public TenantRepository(AccountContext context) : base(context)
    {
    }
}
