using System.Text.Json.Serialization;

namespace Qlt.Services.Accounts.Domain.Models.SubscriptionAggregate;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum SubscriptionStatus
{
    PendingInitialization = 1,
    Active = 2,
    Suspended = 3,
    Cancelled = 4
}
