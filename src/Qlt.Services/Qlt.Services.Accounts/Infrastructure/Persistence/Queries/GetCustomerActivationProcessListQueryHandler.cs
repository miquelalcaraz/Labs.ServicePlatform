using Microsoft.EntityFrameworkCore;

using Qlt.Platform;
using Qlt.Platform.Messaging.Queries;
using Qlt.Services.Accounts.Application.Queries;
using Qlt.Services.Accounts.Domain.Models.CustomerActivationProcessAggregate;

namespace Qlt.Services.Accounts.Infrastructure.Persistence.Queries;

/// <summary>
/// Returns a paginated list of CustomerActivationProcesses with filtering and sorting.
/// </summary>
public class GetCustomerActivationProcessListQueryHandler(AccountReadContext context)
    : IQueryHandler<GetCustomerActivationProcessListQuery, CustomerActivationProcessListResponse>
{
    public async Task<Result<CustomerActivationProcessListResponse>> Handle(
        GetCustomerActivationProcessListQuery request, CancellationToken cancellationToken)
    {
        var query = context.Set<CustomerActivationProcess>().AsQueryable();

        if (!string.IsNullOrWhiteSpace(request.Status) &&
            Enum.TryParse<ActivationStatus>(request.Status, ignoreCase: true, out var statusFilter))
            query = query.Where(e => e.Status == statusFilter);

        if (!string.IsNullOrWhiteSpace(request.CompanyLegalName))
            query = query.Where(e => e.CompanyLegalName != null && e.CompanyLegalName.Contains(request.CompanyLegalName));

        if (!string.IsNullOrWhiteSpace(request.AdminEmail))
            query = query.Where(e => e.AdminEmail != null && e.AdminEmail.Contains(request.AdminEmail));

        var totalCount = await query.CountAsync(cancellationToken);

        query = ApplySorting(query, request.SortBy, request.SortDirection);

        var items = await query
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(cancellationToken);

        var listItems = items.Select(p => new CustomerActivationProcessListItemResponse
        {
            ActivationProcessId = p.Id,
            Status = p.Status.ToString(),
            StatusDisplayName = ActivationDisplayHelpers.GetStatusDisplayName(p.Status),
            CurrentStep = p.CurrentStep.ToString(),
            CurrentStepDisplayName = ActivationDisplayHelpers.GetStepDisplayName(p.CurrentStep),
            CompanyLegalName = p.CompanyLegalName,
            SubscriptionPlanCode = p.SubscriptionPlanCode,
            AdminEmail = p.AdminEmail,
            StartedAtUtc = p.StartedAtUtc,
            RetryCount = p.RetryCount,
            LastErrorCode = p.LastErrorCode,
            Actions = ActivationDisplayHelpers.BuildListActions(p.Status)
        }).ToList();

        return Result.Success(new CustomerActivationProcessListResponse
        {
            Page = request.Page,
            PageSize = request.PageSize,
            TotalCount = totalCount,
            Items = listItems
        });
    }

    private static IQueryable<CustomerActivationProcess> ApplySorting(
        IQueryable<CustomerActivationProcess> query, string sortBy, string sortDirection)
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
