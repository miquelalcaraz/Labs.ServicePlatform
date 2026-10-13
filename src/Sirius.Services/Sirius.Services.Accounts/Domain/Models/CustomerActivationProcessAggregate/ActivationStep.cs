using System.Text.Json.Serialization;

namespace Sirius.Services.Accounts.Domain.Models.CustomerActivationProcessAggregate;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ActivationStep
{
    None = 0,
    ActivationRequested = 1,
    ActivationDelivered = 2,
    UserActivated = 3,
    CustomerReady = 4,
    Completed = 5
}
