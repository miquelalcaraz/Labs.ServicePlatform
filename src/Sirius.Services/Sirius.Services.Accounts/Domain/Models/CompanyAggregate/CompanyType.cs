using System.Text.Json.Serialization;

namespace Sirius.Services.Accounts.Domain.Models.CompanyAggregate;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum CompanyType
{
    Owner = 1,
    Affiliate = 2,
    Branch = 3,
    Partner = 4
}
