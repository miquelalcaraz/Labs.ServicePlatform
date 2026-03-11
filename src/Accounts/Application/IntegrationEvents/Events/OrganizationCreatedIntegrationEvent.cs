using QltSystem.Core.EventBus.Events;

namespace QltSystem.Accounts.Application.IntegrationEvents.Events
{
    public record OrganizationCreatedIntegrationEvent(
        Guid TenantId,
        Guid OrganizationId,
        string Name,
        string LegalName,
        string TaxNumber,
        string CountryCode) : IntegrationEvent;

}
