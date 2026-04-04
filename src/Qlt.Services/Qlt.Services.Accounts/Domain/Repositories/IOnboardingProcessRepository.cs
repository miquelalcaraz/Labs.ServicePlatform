using Qlt.Domain.SeedWork;
using Qlt.Services.Accounts.Domain.Models.OnboardingAggregate;

namespace Qlt.Services.Accounts.Domain.Repositories;

public interface IOnboardingProcessRepository : IRepository<OnboardingProcess>
{
    OnboardingProcess Add(OnboardingProcess onboardingProcess);

    void Update(OnboardingProcess onboardingProcess);

    Task<OnboardingProcess?> GetAsync(Guid onboardingProcessId);

    Task<OnboardingProcess?> GetBySubscriptionIdAsync(Guid subscriptionId);
}
