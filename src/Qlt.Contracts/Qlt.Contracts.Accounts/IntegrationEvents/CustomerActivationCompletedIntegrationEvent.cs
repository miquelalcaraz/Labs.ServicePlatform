using Qlt.Platform.Messaging.IntegrationEvents;

namespace Qlt.Contracts.Accounts.IntegrationEvents;

public record CustomerActivationCompletedIntegrationEvent(
    Guid ActivationProcessId,
    Guid SubscriptionId,
    Guid TenantId,
    Guid OwnerCompanyId,
    Guid AdminUserId,
    Guid AdminMembershipId,
    Guid? CorrelationId) : IntegrationEvent;
