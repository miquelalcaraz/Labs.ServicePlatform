using Qlt.Platform.Messaging.IntegrationEvents;

namespace Qlt.Contracts.Notifications.IntegrationEvents;

public record ActivationDeliveredIntegrationEvent(
    Guid ActivationProcessId,
    Guid UserId,
    string Email,
    DateTimeOffset DeliveredAtUtc,
    Guid? CorrelationId) : IntegrationEvent;
