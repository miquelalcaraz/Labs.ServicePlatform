using Miqa.Platform.Messaging.IntegrationEvents;

namespace Miqa.Contracts.Notifications.IntegrationEvents;

public record ActivationDeliveredIntegrationEvent(
    Guid ActivationProcessId,
    Guid UserId,
    string Email,
    DateTimeOffset DeliveredAtUtc,
    Guid? CorrelationId) : IntegrationEvent;
