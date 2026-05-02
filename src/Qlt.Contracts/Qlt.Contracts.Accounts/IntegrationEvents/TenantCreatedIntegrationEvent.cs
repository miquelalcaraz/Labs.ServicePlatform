using Qlt.Platform.Messaging.IntegrationEvents;

namespace Qlt.Contracts.Accounts.IntegrationEvents;

public record TenantCreatedIntegrationEvent(
    Guid TenantId,
    string Name,
    Guid? OwnerCompanyId,
    Guid CreatedByInternalUserId) : IntegrationEvent;
