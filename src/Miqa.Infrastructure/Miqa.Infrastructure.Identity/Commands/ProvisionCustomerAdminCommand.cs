using Miqa.Platform.Messaging.Commands;

namespace Miqa.Infrastructure.Identity.Commands;

public record ProvisionCustomerAdminCommand(
    Guid SubscriptionId,
    Guid TenantId,
    Guid CompanyId,
    Guid OnboardingProcessId,
    string AdminEmail,
    string AdminFirstName,
    string AdminLastName,
    string TenantName) : ICommand<bool>;
