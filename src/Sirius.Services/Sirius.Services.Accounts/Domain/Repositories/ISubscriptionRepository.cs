using Sirius.Domain.SeedWork;
using Sirius.Services.Accounts.Domain.Models.SubscriptionAggregate;

namespace Sirius.Services.Accounts.Domain.Repositories;

public interface ISubscriptionRepository : IRepository<Subscription>
{
    Subscription Add(Subscription subscription);

    void Update(Subscription subscription);

    Task<Subscription?> GetAsync(Guid subscriptionId);
}
