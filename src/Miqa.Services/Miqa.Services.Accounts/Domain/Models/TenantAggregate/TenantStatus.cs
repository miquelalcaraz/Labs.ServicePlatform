using System.Text.Json.Serialization;

namespace Miqa.Services.Accounts.Domain.Models.TenantAggregate;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum TenantStatus
{
    Pending = 1,
    Active = 2,
    Suspended = 3,
    Closed = 4
}
