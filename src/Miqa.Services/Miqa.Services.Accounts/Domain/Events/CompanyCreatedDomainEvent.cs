using MediatR;

using Miqa.Domain.SeedWork;

namespace Miqa.Services.Accounts.Domain.Events;

public record CompanyCreatedDomainEvent(
    Guid TenantId,
    Guid CompanyId,
    string LegalName,
    string TradeName,
    string TaxId,
    string CountryCode) : IDomainEvent, INotification;
