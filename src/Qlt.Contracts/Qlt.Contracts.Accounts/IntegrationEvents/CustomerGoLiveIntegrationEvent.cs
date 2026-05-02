using Qlt.Platform.Messaging.IntegrationEvents;

namespace Qlt.Contracts.Accounts.IntegrationEvents;

public record CustomerGoLiveIntegrationEvent(
    Guid ActivationProcessId,
    Guid SubscriptionId,
    Guid TenantId,
    Guid? CorrelationId) : IntegrationEvent;
