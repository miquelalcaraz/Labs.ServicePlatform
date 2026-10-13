using Microsoft.EntityFrameworkCore;

using Miqa.Platform;
using Miqa.Platform.Messaging.Queries;
using Miqa.Services.Accounts.Application.Queries;
using Miqa.Services.Accounts.Domain.Models.CustomerActivationProcessAggregate;

namespace Miqa.Services.Accounts.Infrastructure.Persistence.Queries;

/// <summary>
/// Returns the detail view of a CustomerActivationProcess using the read-only context.
/// </summary>
public class GetCustomerActivationProcessByIdQueryHandler(AccountReadContext context)
    : IQueryHandler<GetCustomerActivationProcessByIdQuery, CustomerActivationProcessDetailResponse>
{
    public async Task<Result<CustomerActivationProcessDetailResponse>> Handle(
        GetCustomerActivationProcessByIdQuery request, CancellationToken cancellationToken)
    {
        var process = await context.Set<CustomerActivationProcess>()
            .FirstOrDefaultAsync(e => e.Id == request.Id, cancellationToken);

        if (process is null)
            return Result.Failure<CustomerActivationProcessDetailResponse>(
                Error.NotFound.WithMessage($"Activation process with id '{request.Id}' was not found."));

        return Result.Success(MapToDetail(process));
    }

    private static CustomerActivationProcessDetailResponse MapToDetail(CustomerActivationProcess process)
    {
        var basePath = $"/api/accounts/customer-activation/{process.Id}";

        return new CustomerActivationProcessDetailResponse
        {
            ActivationProcessId = process.Id,
            CorrelationId = process.CorrelationId,
            Status = process.Status.ToString(),
            StatusDisplayName = ActivationDisplayHelpers.GetStatusDisplayName(process.Status),
            CurrentStep = process.CurrentStep.ToString(),
            CurrentStepDisplayName = ActivationDisplayHelpers.GetStepDisplayName(process.CurrentStep),
            ProgressPercent = ActivationDisplayHelpers.CalculateProgressPercent(process.CurrentStep, process.Status),
            RetryCount = process.RetryCount,
            StartedAtUtc = process.StartedAtUtc,
            CompletedAtUtc = process.CompletedAtUtc,
            Summary = new ActivationSummaryResponse
            {
                CompanyLegalName = process.CompanyLegalName,
                SubscriptionPlanCode = process.SubscriptionPlanCode,
                AdminEmail = process.AdminEmail
            },
            References = new ActivationReferencesResponse
            {
                SubscriptionId = process.SubscriptionId,
                TenantId = process.TenantId,
                OwnerCompanyId = process.OwnerCompanyId,
                AdminUserId = process.AdminUserId,
                AdminMembershipId = process.AdminMembershipId
            },
            LastError = process.Status == ActivationStatus.Failed
                ? new ActivationErrorResponse
                {
                    Code = process.LastErrorCode,
                    Message = process.LastErrorMessage
                }
                : null,
            Steps = ActivationDisplayHelpers.BuildStepsList(process.CurrentStep, process.Status),
            Actions = ActivationDisplayHelpers.BuildDetailActions(process.Status)
        };
    }
}
