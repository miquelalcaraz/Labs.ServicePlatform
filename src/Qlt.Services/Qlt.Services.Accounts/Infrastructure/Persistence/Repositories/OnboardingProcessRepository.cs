using Microsoft.EntityFrameworkCore;

using Qlt.Platform.Persistence.Repositories;
using Qlt.Services.Accounts.Domain.Models.OnboardingAggregate;
using Qlt.Services.Accounts.Domain.Repositories;

namespace Qlt.Services.Accounts.Infrastructure.Persistence.Repositories;

public class OnboardingProcessRepository : Repository<OnboardingProcess>, IOnboardingProcessRepository
{
    public OnboardingProcessRepository(AccountContext context) : base(context)
    {
    }

    public async Task<OnboardingProcess?> GetBySubscriptionIdAsync(Guid subscriptionId)
    {
        return await Context.Set<OnboardingProcess>()
            .FirstOrDefaultAsync(e => e.SubscriptionId == subscriptionId);
    }
}
