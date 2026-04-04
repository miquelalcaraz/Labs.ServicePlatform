using Qlt.Platform.Messaging.IntegrationEvents;

namespace Qlt.Services.Accounts.Contracts.IntegrationEvents;

public record CustomerAdminProvisioningRequestedIntegrationEvent(
    Guid SubscriptionId,
    Guid TenantId,
    Guid CompanyId,
    Guid OnboardingProcessId,
    string AdminEmail,
    string AdminFirstName,
    string AdminLastName,
    string TenantName) : IntegrationEvent;
