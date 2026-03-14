using QltSystem.Core.EventBus.Contracts.Events;

namespace QltSystem.Accounts.Contracts.IntegrationEvents.Events
{
    public record OrganizationCreatedIntegrationEvent(
        Guid TenantId,
        Guid OrganizationId,
        string Name,
        string LegalName,
        string TaxNumber,
        string CountryCode) : IntegrationEvent;

}
