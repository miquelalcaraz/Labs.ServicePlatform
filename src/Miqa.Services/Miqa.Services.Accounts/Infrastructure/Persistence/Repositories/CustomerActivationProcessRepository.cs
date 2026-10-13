using Microsoft.EntityFrameworkCore;

using Miqa.Platform.Persistence.Repositories;
using Miqa.Services.Accounts.Domain.Models.CustomerActivationProcessAggregate;
using Miqa.Services.Accounts.Domain.Repositories;

namespace Miqa.Services.Accounts.Infrastructure.Persistence.Repositories;

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
