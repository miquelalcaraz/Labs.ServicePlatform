using Microsoft.EntityFrameworkCore;

using Qlt.Platform.Persistence.Repositories;
using Qlt.Services.Accounts.Domain.Models.CustomerActivationProcessAggregate;
using Qlt.Services.Accounts.Domain.Repositories;

namespace Qlt.Services.Accounts.Infrastructure.Persistence.Repositories;

public class CustomerActivationProcessRepository : Repository<CustomerActivationProcess>, ICustomerActivationProcessRepository
{
    public CustomerActivationProcessRepository(AccountContext context) : base(context)
    {
    }

    public async Task<CustomerActivationProcess?> GetBySubscriptionIdAsync(Guid subscriptionId)
    {
        return await Context.Set<CustomerActivationProcess>()
            .FirstOrDefaultAsync(e => e.SubscriptionId == subscriptionId);
    }

    public async Task<CustomerActivationProcess?> GetByAdminUserIdAsync(Guid adminUserId)
    {
        return await Context.Set<CustomerActivationProcess>()
            .FirstOrDefaultAsync(e => e.AdminUserId == adminUserId);
    }
}
