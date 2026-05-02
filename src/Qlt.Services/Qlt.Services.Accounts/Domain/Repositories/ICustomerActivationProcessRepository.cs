using Qlt.Domain.SeedWork;
using Qlt.Services.Accounts.Domain.Models.CustomerActivationProcessAggregate;

namespace Qlt.Services.Accounts.Domain.Repositories;

public interface ICustomerActivationProcessRepository : IRepository<CustomerActivationProcess>
{
    CustomerActivationProcess Add(CustomerActivationProcess activationProcess);

    void Update(CustomerActivationProcess activationProcess);

    Task<CustomerActivationProcess?> GetAsync(Guid activationProcessId);

    Task<CustomerActivationProcess?> GetBySubscriptionIdAsync(Guid subscriptionId);

    Task<CustomerActivationProcess?> GetByAdminUserIdAsync(Guid adminUserId);
}
