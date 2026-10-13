using System.Text.Json.Serialization;

namespace Sirius.Services.Accounts.Domain.Models.OnboardingAggregate;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum OnboardingStep
{
    None = 0,
    SubscriptionCreated = 1,
    TenantCreated = 2,
    OwnerCompanyCreated = 3,
    AdminProvisioningRequested = 4,
    AdminUserProvisioned = 5,
    AdminMembershipCreated = 6,
    Completed = 9
}
