using System.Text.Json.Serialization;

namespace Sirius.Services.Accounts.Domain.Models.UserMembershipAggregate;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum MembershipRole
{
    TenantAdmin = 1,
    CompanyAdmin = 2,
    CompanyManager = 3,
    CompanyUser = 4
}
