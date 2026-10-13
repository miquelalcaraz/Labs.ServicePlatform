using Sirius.Domain.SeedWork;
using Sirius.Services.Accounts.Domain.Models.OnboardingAggregate;

namespace Sirius.Services.Accounts.Domain.Repositories;

public interface IOnboardingProcessRepository : IRepository<OnboardingProcess>
{
    OnboardingProcess Add(OnboardingProcess onboardingProcess);

    void Update(OnboardingProcess onboardingProcess);

    Task<OnboardingProcess?> GetAsync(Guid onboardingProcessId);

    Task<OnboardingProcess?> GetBySubscriptionIdAsync(Guid subscriptionId);
}
