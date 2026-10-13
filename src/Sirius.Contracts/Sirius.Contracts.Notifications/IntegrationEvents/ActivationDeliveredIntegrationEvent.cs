using Sirius.Platform.Messaging.IntegrationEvents;

namespace Sirius.Contracts.Notifications.IntegrationEvents;

public record ActivationDeliveredIntegrationEvent(
    Guid ActivationProcessId,
    Guid UserId,
    string Email,
    DateTimeOffset DeliveredAtUtc,
    Guid? CorrelationId) : IntegrationEvent;
