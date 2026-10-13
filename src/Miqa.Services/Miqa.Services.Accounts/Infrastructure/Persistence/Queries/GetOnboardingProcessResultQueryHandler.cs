using Microsoft.EntityFrameworkCore;

using Miqa.Platform;
using Miqa.Platform.Messaging.Queries;
using Miqa.Services.Accounts.Application.Queries;
using Miqa.Services.Accounts.Domain.Models.CompanyAggregate;
using Miqa.Services.Accounts.Domain.Models.OnboardingAggregate;
using Miqa.Services.Accounts.Domain.Models.SubscriptionAggregate;
using Miqa.Services.Accounts.Domain.Models.TenantAggregate;
using Miqa.Services.Accounts.Domain.Models.UserMembershipAggregate;

namespace Miqa.Services.Accounts.Infrastructure.Persistence.Queries;

/// <summary>
/// Handles GetOnboardingProcessResultQuery by joining OnboardingProcess with related entities.
/// </summary>
public class GetOnboardingProcessResultQueryHandler(AccountReadContext context)
    : IQueryHandler<GetOnboardingProcessResultQuery, OnboardingProcessResultResponse>
{
    public async Task<Result<OnboardingProcessResultResponse>> Handle(
        GetOnboardingProcessResultQuery request, CancellationToken cancellationToken)
    {
        var onboarding = await context.Set<OnboardingProcess>()
            .FirstOrDefaultAsync(e => e.Id == request.Id, cancellationToken);

        if (onboarding is null)
            return Result.Failure<OnboardingProcessResultResponse>(
                Error.NotFound.WithMessage($"Onboarding process with id '{request.Id}' was not found."));

        if (onboarding.Status != OnboardingStatus.Completed)
            return Result.Failure<OnboardingProcessResultResponse>(
                Error.Conflict.WithMessage("The onboarding result is not available until the process is completed."));

        var subscription = await context.Set<Subscription>()
            .FirstOrDefaultAsync(s => s.Id == onboarding.SubscriptionId, cancellationToken);

        var tenant = await context.Set<Tenant>()
            .FirstOrDefaultAsync(t => t.Id == onboarding.TenantId, cancellationToken);

        var company = onboarding.OwnerCompanyId.HasValue
            ? await context.Set<Company>()
                .FirstOrDefaultAsync(c => c.Id == onboarding.OwnerCompanyId.Value, cancellationToken)
            : null;

        UserMembership? membership = null;
        if (onboarding.AdminMembershipId.HasValue)
        {
            membership = await context.Set<UserMembership>()
                .FirstOrDefaultAsync(m => m.Id == onboarding.AdminMembershipId.Value, cancellationToken);
        }

        var basePath = "/api/accounts/customer-onboarding";

        return Result.Success(new OnboardingProcessResultResponse
        {
            OnboardingProcessId = onboarding.Id,
            Status = onboarding.Status.ToString(),
            Subscription = new OnboardingResultSubscriptionResponse
            {
                SubscriptionId = subscription?.Id ?? onboarding.SubscriptionId,
                SubscriptionPlanCode = subscription?.PlanCode ?? onboarding.SubscriptionPlanCode,
                Status = subscription?.Status.ToString() ?? "Unknown"
            },
            Tenant = new OnboardingResultTenantResponse
            {
                TenantId = tenant?.Id ?? onboarding.TenantId,
                Name = tenant?.Name ?? onboarding.TenantName ?? "Unknown",
                Status = tenant?.Status.ToString() ?? "Unknown"
            },
            OwnerCompany = new OnboardingResultCompanyResponse
            {
                CompanyId = company?.Id ?? onboarding.OwnerCompanyId ?? Guid.Empty,
                LegalName = company?.LegalName ?? onboarding.CompanyLegalName ?? "Unknown",
                TradeName = company?.TradeName ?? string.Empty,
                CompanyType = company?.CompanyType.ToString() ?? "Owner",
                Status = company?.Status.ToString() ?? "Unknown"
            },
            AdminUser = onboarding.AdminUserId.HasValue
                ? new OnboardingResultAdminUserResponse
                {
                    UserId = onboarding.AdminUserId.Value,
                    Email = onboarding.AdminEmail,
                    FirstName = onboarding.AdminFirstName,
                    LastName = onboarding.AdminLastName
                }
                : null,
            AdminMembership = membership is not null
                ? new OnboardingResultMembershipResponse
                {
                    MembershipId = membership.Id,
                    TenantId = membership.TenantId,
                    CompanyId = membership.CompanyId,
                    RoleCode = membership.RoleCode.ToString(),
                    Status = membership.Status.ToString()
                }
                : null,
            Links = new OnboardingLinksResponse
            {
                Self = $"{basePath}/{onboarding.Id}/result"
            }
        });
    }
}
