using Sirius.Platform.Messaging.IntegrationEvents;

namespace Sirius.Contracts.Accounts.IntegrationEvents;

public record CompanyCreatedIntegrationEvent(
    Guid TenantId,
    Guid CompanyId,
    string LegalName,
    string TradeName,
    string TaxId,
    string CountryCode) : IntegrationEvent;
