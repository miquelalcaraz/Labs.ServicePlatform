using Qlt.Platform.Messaging.IntegrationEvents;

namespace Qlt.Services.Accounts.Contracts.IntegrationEvents;

public record SubscriptionCreatedIntegrationEvent(
    Guid SubscriptionId,
    Guid TenantId,
    string PlanCode,
    Guid CreatedByInternalUserId) : IntegrationEvent;
