using Miqa.Platform.Messaging.IntegrationEvents;

namespace Miqa.Contracts.Accounts.IntegrationEvents;

public record CustomerGoLiveIntegrationEvent(
    Guid ActivationProcessId,
    Guid SubscriptionId,
    Guid TenantId,
    Guid? CorrelationId) : IntegrationEvent;
