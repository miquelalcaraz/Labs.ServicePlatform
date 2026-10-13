using System.Text.Json.Serialization;

namespace Sirius.Services.Accounts.Domain.Models.UserMembershipAggregate;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum MembershipType
{
    Internal = 1,
    External = 2
}
