using System.Text.Json.Serialization;

namespace Sirius.Services.Accounts.Domain.Models.CompanyAggregate;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum CompanyStatus
{
    Pending = 1,
    Active = 2,
    Inactive = 3
}
