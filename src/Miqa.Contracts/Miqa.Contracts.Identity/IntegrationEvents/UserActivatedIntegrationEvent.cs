using Miqa.Platform.Messaging.IntegrationEvents;

namespace Miqa.Contracts.Identity.IntegrationEvents;

public record UserActivatedIntegrationEvent(
    Guid UserId,
    string Email,
    Guid? TenantId,
    DateTimeOffset ActivatedAtUtc,
    Guid? CorrelationId) : IntegrationEvent;
