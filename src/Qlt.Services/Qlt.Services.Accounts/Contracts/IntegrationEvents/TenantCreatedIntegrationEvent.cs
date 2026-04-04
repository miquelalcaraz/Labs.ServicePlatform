using Qlt.Platform.Messaging.IntegrationEvents;

namespace Qlt.Services.Accounts.Contracts.IntegrationEvents;

public record TenantCreatedIntegrationEvent(
    Guid TenantId,
    string Name,
    Guid? OwnerCompanyId,
    Guid CreatedByInternalUserId) : IntegrationEvent;
