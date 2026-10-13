using Microsoft.EntityFrameworkCore;

using Sirius.Platform.Persistence.Repositories;
using Sirius.Services.Accounts.Domain.Models.OnboardingAggregate;
using Sirius.Services.Accounts.Domain.Repositories;

namespace Sirius.Services.Accounts.Infrastructure.Persistence.Repositories;

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
