using Miqa.Platform.Messaging.IntegrationEvents;

namespace Miqa.Contracts.Accounts.IntegrationEvents;

public record CustomerAdminProvisioningRequestedIntegrationEvent(
    Guid SubscriptionId,
    Guid TenantId,
    Guid CompanyId,
    Guid OnboardingProcessId,
    string AdminEmail,
    string AdminFirstName,
    string AdminLastName,
    string TenantName) : IntegrationEvent;
