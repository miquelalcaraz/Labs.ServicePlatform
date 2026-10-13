using Sirius.Platform.Messaging.IntegrationEvents;

namespace Sirius.Contracts.Identity.IntegrationEvents;

public record UserActivatedIntegrationEvent(
    Guid UserId,
    string Email,
    Guid? TenantId,
    DateTimeOffset ActivatedAtUtc,
    Guid? CorrelationId) : IntegrationEvent;
