using QltS.Core.Domain.Messaging;

namespace QltS.Accounts.Domain.Events
{
    public record OrganizationCreatedDomainEvent(
        Guid TenantId,
        Guid OrganizationId,
        string Name,
        string LegalName,
        string TaxNumber,
        string CountryCode) : IDomainEvent;

}
