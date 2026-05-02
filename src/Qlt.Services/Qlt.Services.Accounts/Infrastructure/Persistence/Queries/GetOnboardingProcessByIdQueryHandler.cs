using Microsoft.EntityFrameworkCore;

using Qlt.Platform;
using Qlt.Platform.Messaging.Queries;
using Qlt.Services.Accounts.Application.Queries;
using Qlt.Services.Accounts.Domain.Models.OnboardingAggregate;

namespace Qlt.Services.Accounts.Infrastructure.Persistence.Queries;

/// <summary>
/// Handles GetOnboardingProcessByIdQuery using the read-only AccountReadContext.
/// </summary>
public class GetOnboardingProcessByIdQueryHandler(AccountReadContext context)
    : IQueryHandler<GetOnboardingProcessByIdQuery, OnboardingProcessDetailResponse>
{
    public async Task<Result<OnboardingProcessDetailResponse>> Handle(
        GetOnboardingProcessByIdQuery request, CancellationToken cancellationToken)
    {
        var onboarding = await context.Set<OnboardingProcess>()
            .FirstOrDefaultAsync(e => e.Id == request.Id, cancellationToken);

        if (onboarding is null)
            return Result.Failure<OnboardingProcessDetailResponse>(
                Error.NotFound.WithMessage($"Onboarding process with id '{request.Id}' was not found."));

        var basePath = $"/api/accounts/customer-onboarding/{onboarding.Id}";

        return Result.Success(new OnboardingProcessDetailResponse
        {
            OnboardingProcessId = onboarding.Id,
            CorrelationId = onboarding.CorrelationId,
            Status = onboarding.Status.ToString(),
            StatusDisplayName = OnboardingDisplayHelpers.GetStatusDisplayName(onboarding.Status),
            CurrentStep = onboarding.CurrentStep.ToString(),
            CurrentStepDisplayName = OnboardingDisplayHelpers.GetStepDisplayName(onboarding.CurrentStep),
            ProgressPercent = OnboardingDisplayHelpers.CalculateProgressPercent(onboarding.CurrentStep, onboarding.Status),
            RetryCount = onboarding.RetryCount,
            StartedAtUtc = onboarding.StartedAtUtc,
            CompletedAtUtc = onboarding.CompletedAtUtc,
            FailedAtUtc = onboarding.FailedAtUtc,
            Summary = new OnboardingSummaryResponse
            {
                CompanyLegalName = onboarding.CompanyLegalName,
                SubscriptionPlanCode = onboarding.SubscriptionPlanCode,
                AdminEmail = onboarding.AdminEmail
            },
            References = new OnboardingReferencesResponse
            {
                SubscriptionId = onboarding.SubscriptionId,
                TenantId = onboarding.TenantId,
                OwnerCompanyId = onboarding.OwnerCompanyId,
                AdminUserId = onboarding.AdminUserId,
                AdminMembershipId = onboarding.AdminMembershipId
            },
            LastError = onboarding.Status == OnboardingStatus.Failed
                ? new OnboardingErrorResponse
                {
                    Code = onboarding.ErrorCode,
                    Message = onboarding.ErrorMessage,
                    FailedStep = onboarding.FailedStep?.ToString(),
                    OccurredAtUtc = onboarding.FailedAtUtc
                }
                : null,
            Steps = OnboardingDisplayHelpers.BuildStepsList(onboarding.CurrentStep, onboarding.Status, onboarding.FailedStep),
            Actions = OnboardingDisplayHelpers.BuildDetailActions(onboarding.Status)
        });
    }
}
