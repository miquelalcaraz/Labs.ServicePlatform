using Miqa.Platform.Messaging.Commands;

namespace Miqa.Services.Accounts.Application.Commands;

public record CompleteOnboardingCommand(
    Guid OnboardingProcessId,
    Guid UserId,
    Guid TenantId,
    Guid CompanyId,
    Guid SubscriptionId,
    string Email) : ICommand<CompleteOnboardingResult>;

public record CompleteOnboardingResult(
    Guid AdminMembershipId,
    string? CompanyLegalName,
    string? SubscriptionPlanCode);
