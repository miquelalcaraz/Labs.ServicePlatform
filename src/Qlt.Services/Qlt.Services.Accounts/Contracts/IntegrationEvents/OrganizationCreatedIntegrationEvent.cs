using Qlt.Platform.Messaging.IntegrationEvents;

namespace Qlt.Services.Accounts.Contracts.IntegrationEvents
{
    public record OrganizationCreatedIntegrationEvent(
        Guid TenantId,
        Guid OrganizationId,
        string Name,
        string LegalName,
        string TaxNumber,
        string CountryCode) : IntegrationEvent;

}
