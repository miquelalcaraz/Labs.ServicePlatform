using System.Text.Json.Serialization;

namespace Qlt.Infrastructure.Identity.Models;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum UserStatus
{
    PendingActivation = 1,
    Active = 2,
    Locked = 3,
    Disabled = 4
}
