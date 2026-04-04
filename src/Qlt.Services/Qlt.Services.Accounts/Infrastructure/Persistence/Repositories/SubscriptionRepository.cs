using Qlt.Platform.Persistence.Repositories;
using Qlt.Services.Accounts.Domain.Models.SubscriptionAggregate;
using Qlt.Services.Accounts.Domain.Repositories;

namespace Qlt.Services.Accounts.Infrastructure.Persistence.Repositories;

public class SubscriptionRepository : Repository<Subscription>, ISubscriptionRepository
{
    public SubscriptionRepository(AccountContext context) : base(context)
    {
    }
}
