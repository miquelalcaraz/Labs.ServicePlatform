using Miqa.Platform.Messaging.IntegrationEvents;

namespace Miqa.Contracts.Accounts.IntegrationEvents;

public record CompanyCreatedIntegrationEvent(
    Guid TenantId,
    Guid CompanyId,
    string LegalName,
    string TradeName,
    string TaxId,
    string CountryCode) : IntegrationEvent;
