using Sirius.Platform.Messaging.IntegrationEvents;

namespace Sirius.Contracts.Identity.IntegrationEvents;

public record ExternalUserProvisionedIntegrationEvent(
    Guid UserId,
    Guid TenantId,
    Guid CompanyId,
    Guid SubscriptionId,
    Guid OnboardingProcessId,
    string Email,
    string FirstName,
    string LastName) : IntegrationEvent;
