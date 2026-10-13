using Sirius.Platform.Messaging.IntegrationEvents;

namespace Sirius.Contracts.Accounts.IntegrationEvents;

public record ActivationEmailRequestedIntegrationEvent(
    Guid ActivationProcessId,
    Guid SubscriptionId,
    Guid TenantId,
    Guid OwnerCompanyId,
    Guid AdminUserId,
    string AdminEmail,
    string? CompanyLegalName,
    string? SubscriptionPlanCode,
    Guid? CorrelationId) : IntegrationEvent;
