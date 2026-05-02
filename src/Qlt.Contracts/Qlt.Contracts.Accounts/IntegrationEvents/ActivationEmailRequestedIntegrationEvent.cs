using Qlt.Platform.Messaging.IntegrationEvents;

namespace Qlt.Contracts.Accounts.IntegrationEvents;

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
