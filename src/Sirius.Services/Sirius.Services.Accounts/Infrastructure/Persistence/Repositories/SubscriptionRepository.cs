using Sirius.Platform.Persistence.Repositories;
using Sirius.Services.Accounts.Domain.Models.SubscriptionAggregate;
using Sirius.Services.Accounts.Domain.Repositories;

namespace Sirius.Services.Accounts.Infrastructure.Persistence.Repositories;

public class SubscriptionRepository : Repository<Subscription>, ISubscriptionRepository
{
    public SubscriptionRepository(AccountContext context) : base(context)
    {
    }
}
