using System.Text.Json.Serialization;

namespace Sirius.Services.Accounts.Domain.Models.OnboardingAggregate;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum OnboardingStatus
{
    Draft = 1,
    InProgress = 2,
    WaitingForExternalProvisioning = 3,
    Completed = 6,
    Failed = 7
}
