using System.Text.Json.Serialization;

namespace Sirius.Services.Accounts.Domain.Models.UserMembershipAggregate;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum MembershipStatus
{
    PendingActivation = 1,
    Active = 2,
    Inactive = 3,
    Revoked = 4
}
