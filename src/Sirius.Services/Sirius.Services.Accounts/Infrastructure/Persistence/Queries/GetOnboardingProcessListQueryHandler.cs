using Microsoft.EntityFrameworkCore;

using Sirius.Platform;
using Sirius.Platform.Messaging.Queries;
using Sirius.Services.Accounts.Application.Queries;
using Sirius.Services.Accounts.Domain.Models.OnboardingAggregate;

namespace Sirius.Services.Accounts.Infrastructure.Persistence.Queries;

/// <summary>
/// Handles GetOnboardingProcessListQuery with filtering, sorting and pagination.
/// </summary>
public class GetOnboardingProcessListQueryHandler(AccountReadContext context)
    : IQueryHandler<GetOnboardingProcessListQuery, OnboardingProcessListResponse>
{
    public async Task<Result<OnboardingProcessListResponse>> Handle(
        GetOnboardingProcessListQuery request, CancellationToken cancellationToken)
    {
        var query = context.Set<OnboardingProcess>().AsQueryable();

        if (!string.IsNullOrWhiteSpace(request.Status) &&
            Enum.TryParse<OnboardingStatus>(request.Status, ignoreCase: true, out var statusFilter))
        {
            query = query.Where(e => e.Status == statusFilter);
        }

        if (!string.IsNullOrWhiteSpace(request.CompanyLegalName))
            query = query.Where(e => e.CompanyLegalName != null && e.CompanyLegalName.Contains(request.CompanyLegalName));

        if (!string.IsNullOrWhiteSpace(request.AdminEmail))
            query = query.Where(e => e.AdminEmail != null && e.AdminEmail.Contains(request.AdminEmail));

        if (!string.IsNullOrWhiteSpace(request.SubscriptionPlanCode))
            query = query.Where(e => e.SubscriptionPlanCode != null && e.SubscriptionPlanCode.Contains(request.SubscriptionPlanCode));

        var totalCount = await query.CountAsync(cancellationToken);

        query = ApplySorting(query, request.SortBy, request.SortDirection);

        var items = await query
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(cancellationToken);

        var listItems = items.Select(o => new OnboardingProcessListItemResponse
        {
            OnboardingProcessId = o.Id,
            Status = o.Status.ToString(),
            StatusDisplayName = OnboardingDisplayHelpers.GetStatusDisplayName(o.Status),
            CurrentStep = o.CurrentStep.ToString(),
            CurrentStepDisplayName = OnboardingDisplayHelpers.GetStepDisplayName(o.CurrentStep),
            CompanyLegalName = o.CompanyLegalName,
            SubscriptionPlanCode = o.SubscriptionPlanCode,
            AdminEmail = o.AdminEmail,
            StartedAtUtc = o.StartedAtUtc,
            RetryCount = o.RetryCount,
            LastErrorCode = o.ErrorCode,
            Actions = OnboardingDisplayHelpers.BuildListActions(o.Status)
        }).ToList();

        return Result.Success(new OnboardingProcessListResponse
        {
            Page = request.Page,
            PageSize = request.PageSize,
            TotalCount = totalCount,
            Items = listItems
        });
    }

    private static IQueryable<OnboardingProcess> ApplySorting(
        IQueryable<OnboardingProcess> query, string sortBy, string sortDirection)
    {
        var descending = sortDirection.Equals("desc", StringComparison.OrdinalIgnoreCase);

        return sortBy.ToLowerInvariant() switch
        {
            "status" => descending
                ? query.OrderByDescending(e => e.Status)
                : query.OrderBy(e => e.Status),
            "companylegalname" => descending
                ? query.OrderByDescending(e => e.CompanyLegalName)
                : query.OrderBy(e => e.CompanyLegalName),
            "adminemail" => descending
                ? query.OrderByDescending(e => e.AdminEmail)
                : query.OrderBy(e => e.AdminEmail),
            "retrycount" => descending
                ? query.OrderByDescending(e => e.RetryCount)
                : query.OrderBy(e => e.RetryCount),
            _ => descending
                ? query.OrderByDescending(e => e.StartedAtUtc)
                : query.OrderBy(e => e.StartedAtUtc)
        };
    }
}
