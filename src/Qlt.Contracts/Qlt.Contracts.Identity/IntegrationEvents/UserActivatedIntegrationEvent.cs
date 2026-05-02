using Qlt.Platform.Messaging.IntegrationEvents;

namespace Qlt.Contracts.Identity.IntegrationEvents;

public record UserActivatedIntegrationEvent(
    Guid UserId,
    string Email,
    Guid? TenantId,
    DateTimeOffset ActivatedAtUtc,
    Guid? CorrelationId) : IntegrationEvent;
