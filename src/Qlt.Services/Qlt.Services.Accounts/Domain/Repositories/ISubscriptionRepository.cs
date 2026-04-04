using Qlt.Domain.SeedWork;
using Qlt.Services.Accounts.Domain.Models.SubscriptionAggregate;

namespace Qlt.Services.Accounts.Domain.Repositories;

public interface ISubscriptionRepository : IRepository<Subscription>
{
    Subscription Add(Subscription subscription);

    void Update(Subscription subscription);

    Task<Subscription?> GetAsync(Guid subscriptionId);
}
