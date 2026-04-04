using Qlt.Platform.Messaging.IntegrationEvents;

namespace Qlt.Services.Accounts.Contracts.IntegrationEvents;

public record CompanyCreatedIntegrationEvent(
    Guid TenantId,
    Guid CompanyId,
    string LegalName,
    string TradeName,
    string TaxId,
    string CountryCode) : IntegrationEvent;
