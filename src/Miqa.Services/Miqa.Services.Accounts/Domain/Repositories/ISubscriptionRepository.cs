using Miqa.Domain.SeedWork;
using Miqa.Services.Accounts.Domain.Models.SubscriptionAggregate;

namespace Miqa.Services.Accounts.Domain.Repositories;

public interface ISubscriptionRepository : IRepository<Subscription>
{
    Subscription Add(Subscription subscription);

    void Update(Subscription subscription);

    Task<Subscription?> GetAsync(Guid subscriptionId);
}
