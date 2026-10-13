using System.Text.Json.Serialization;

namespace Miqa.Services.Accounts.Domain.Models.CustomerActivationProcessAggregate;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ActivationStatus
{
    Draft = 0,
    InProgress = 1,
    WaitingForNotificationDelivery = 2,
    WaitingForUserActivation = 3,
    Completed = 4,
    Failed = 5
}
