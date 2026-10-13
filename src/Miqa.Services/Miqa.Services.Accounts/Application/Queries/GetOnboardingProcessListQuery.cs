using Miqa.Platform.Messaging.Queries;

namespace Miqa.Services.Accounts.Application.Queries;

/// <summary>
/// Retrieves a paginated list of onboarding processes with optional filtering.
/// </summary>
public record GetOnboardingProcessListQuery(
    string? Status = null,
    string? CompanyLegalName = null,
    string? AdminEmail = null,
    string? SubscriptionPlanCode = null,
    int Page = 1,
    int PageSize = 20,
    string SortBy = "startedAtUtc",
    string SortDirection = "desc") : IQuery<OnboardingProcessListResponse>;
