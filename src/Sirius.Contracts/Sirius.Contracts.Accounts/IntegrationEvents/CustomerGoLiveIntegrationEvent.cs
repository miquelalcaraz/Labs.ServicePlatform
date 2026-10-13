using Sirius.Platform.Messaging.IntegrationEvents;

namespace Sirius.Contracts.Accounts.IntegrationEvents;

public record CustomerGoLiveIntegrationEvent(
    Guid ActivationProcessId,
    Guid SubscriptionId,
    Guid TenantId,
    Guid? CorrelationId) : IntegrationEvent;
