using MediatR;
using Qlt.Domain;

namespace Qlt.Services.Accounts.Domain.Events
{
    public record OrganizationCreatedDomainEvent(
        Guid TenantId,
        Guid OrganizationId,
        string Name,
        string LegalName,
        string TaxNumber,
        string CountryCode) : IDomainEvent, INotification;

}
