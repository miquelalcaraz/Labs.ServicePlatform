using Sirius.Platform.Messaging.IntegrationEvents;

namespace Sirius.Contracts.Accounts.IntegrationEvents;

public record CustomerActivationCompletedIntegrationEvent(
    Guid ActivationProcessId,
    Guid SubscriptionId,
    Guid TenantId,
    Guid OwnerCompanyId,
    Guid AdminUserId,
    Guid AdminMembershipId,
    Guid? CorrelationId) : IntegrationEvent;
