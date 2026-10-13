using Miqa.Platform.Messaging.IntegrationEvents;

namespace Miqa.Contracts.Accounts.IntegrationEvents;

public record TenantCreatedIntegrationEvent(
    Guid TenantId,
    string Name,
    Guid? OwnerCompanyId,
    Guid CreatedByInternalUserId) : IntegrationEvent;
