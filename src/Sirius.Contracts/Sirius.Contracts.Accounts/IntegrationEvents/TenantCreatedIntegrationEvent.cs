using Sirius.Platform.Messaging.IntegrationEvents;

namespace Sirius.Contracts.Accounts.IntegrationEvents;

public record TenantCreatedIntegrationEvent(
    Guid TenantId,
    string Name,
    Guid? OwnerCompanyId,
    Guid CreatedByInternalUserId) : IntegrationEvent;
