using System.Text.Json.Serialization;

namespace Qlt.Services.Accounts.Domain.Models.OnboardingAggregate;

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
    ActivationRequested = 7,
    ActivationDelivered = 8,
    Completed = 9
}
