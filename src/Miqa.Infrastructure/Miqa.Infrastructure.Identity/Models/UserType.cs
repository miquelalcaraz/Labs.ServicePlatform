using System.Text.Json.Serialization;

namespace Miqa.Infrastructure.Identity.Models;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum UserType
{
    Internal = 1,
    External = 2
}
