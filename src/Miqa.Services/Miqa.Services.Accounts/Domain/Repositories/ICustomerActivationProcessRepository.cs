using Miqa.Domain.SeedWork;
using Miqa.Services.Accounts.Domain.Models.CustomerActivationProcessAggregate;

namespace Miqa.Services.Accounts.Domain.Repositories;

public interface ICustomerActivationProcessRepository : IRepository<CustomerActivationProcess>
{
    CustomerActivationProcess Add(CustomerActivationProcess activationProcess);

    void Update(CustomerActivationProcess activationProcess);

    Task<CustomerActivationProcess?> GetAsync(Guid activationProcessId);

    Task<CustomerActivationProcess?> GetBySubscriptionIdAsync(Guid subscriptionId);

    Task<CustomerActivationProcess?> GetByAdminUserIdAsync(Guid adminUserId);
}
