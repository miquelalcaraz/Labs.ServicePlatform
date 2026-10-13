using System.Text.Json.Serialization;

namespace Miqa.Services.Accounts.Domain.Models.CompanyAggregate;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum CompanyStatus
{
    Pending = 1,
    Active = 2,
    Inactive = 3
}
