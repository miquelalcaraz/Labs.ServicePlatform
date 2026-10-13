using MediatR;

using Sirius.Domain.SeedWork;

namespace Sirius.Services.Accounts.Domain.Events;

public record CompanyCreatedDomainEvent(
    Guid TenantId,
    Guid CompanyId,
    string LegalName,
    string TradeName,
    string TaxId,
    string CountryCode) : IDomainEvent, INotification;
