using Microsoft.EntityFrameworkCore;

using Miqa.Platform.Persistence.Repositories;
using Miqa.Services.Accounts.Domain.Models.OnboardingAggregate;
using Miqa.Services.Accounts.Domain.Repositories;

namespace Miqa.Services.Accounts.Infrastructure.Persistence.Repositories;

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
