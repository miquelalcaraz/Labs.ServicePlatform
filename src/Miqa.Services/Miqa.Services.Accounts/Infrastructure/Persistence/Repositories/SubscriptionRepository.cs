using Miqa.Platform.Persistence.Repositories;
using Miqa.Services.Accounts.Domain.Models.SubscriptionAggregate;
using Miqa.Services.Accounts.Domain.Repositories;

namespace Miqa.Services.Accounts.Infrastructure.Persistence.Repositories;

public class SubscriptionRepository : Repository<Subscription>, ISubscriptionRepository
{
    public SubscriptionRepository(AccountContext context) : base(context)
    {
    }
}
