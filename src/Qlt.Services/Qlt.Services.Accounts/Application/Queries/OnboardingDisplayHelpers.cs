using Qlt.Services.Accounts.Domain.Models.OnboardingAggregate;

namespace Qlt.Services.Accounts.Application.Queries;

public static class OnboardingDisplayHelpers
{
    private static readonly OnboardingStep[] OrderedSteps =
    [
        OnboardingStep.SubscriptionCreated,
        OnboardingStep.TenantCreated,
        OnboardingStep.OwnerCompanyCreated,
        OnboardingStep.AdminProvisioningRequested,
        OnboardingStep.AdminUserProvisioned,
        OnboardingStep.AdminMembershipCreated,
        OnboardingStep.Completed
    ];

    public static string GetStatusDisplayName(OnboardingStatus status) => status switch
    {
        OnboardingStatus.Draft => "Draft",
        OnboardingStatus.InProgress => "In Progress",
        OnboardingStatus.WaitingForExternalProvisioning => "Waiting for Identity Provisioning",
        OnboardingStatus.Completed => "Completed",
        OnboardingStatus.Failed => "Failed",
        _ => status.ToString()
    };

    public static string GetStepDisplayName(OnboardingStep step) => step switch
    {
        OnboardingStep.None => "Not Started",
        OnboardingStep.SubscriptionCreated => "Subscription Created",
        OnboardingStep.TenantCreated => "Tenant Created",
        OnboardingStep.OwnerCompanyCreated => "Owner Company Created",
        OnboardingStep.AdminProvisioningRequested => "Admin Provisioning Requested",
        OnboardingStep.AdminUserProvisioned => "Admin User Provisioned",
        OnboardingStep.AdminMembershipCreated => "Admin Membership Created",
        OnboardingStep.Completed => "Completed",
        _ => step.ToString()
    };

    public static int CalculateProgressPercent(OnboardingStep currentStep, OnboardingStatus status)
    {
        if (status == OnboardingStatus.Completed)
            return 100;

        return currentStep switch
        {
            OnboardingStep.None => 0,
            OnboardingStep.SubscriptionCreated => 15,
            OnboardingStep.TenantCreated => 30,
            OnboardingStep.OwnerCompanyCreated => 45,
            OnboardingStep.AdminProvisioningRequested => 55,
            OnboardingStep.AdminUserProvisioned => 70,
            OnboardingStep.AdminMembershipCreated => 85,
            OnboardingStep.Completed => 100,
            _ => 0
        };
    }

    public static string GetStepStatus(OnboardingStep step, OnboardingStep currentStep, OnboardingStatus status, OnboardingStep? failedStep)
    {
        if (status == OnboardingStatus.Failed && failedStep == step)
            return "Failed";

        if (step == OnboardingStep.Completed && status == OnboardingStatus.Completed)
            return "Completed";

        var stepOrder = (int)step;
        var currentOrder = (int)currentStep;

        if (stepOrder < currentOrder)
            return "Completed";

        if (stepOrder == currentOrder)
            return status == OnboardingStatus.Failed ? "Failed" : "Current";

        return "Pending";
    }

    public static IReadOnlyList<OnboardingStepResponse> BuildStepsList(OnboardingStep currentStep, OnboardingStatus status, OnboardingStep? failedStep)
    {
        return OrderedSteps.Select(step => new OnboardingStepResponse
        {
            Code = step.ToString(),
            DisplayName = GetStepDisplayName(step),
            Status = GetStepStatus(step, currentStep, status, failedStep)
        }).ToList();
    }

    public static OnboardingDetailActionsResponse BuildDetailActions(OnboardingStatus status)
    {
        return new OnboardingDetailActionsResponse
        {
            CanRetry = status == OnboardingStatus.Failed,
            CanRefresh = status is not (OnboardingStatus.Completed or OnboardingStatus.Failed),
            CanOpenCustomer = status == OnboardingStatus.Completed
        };
    }

    public static OnboardingListActionsResponse BuildListActions(OnboardingStatus status)
    {
        return new OnboardingListActionsResponse
        {
            CanOpen = true,
            CanRetry = status == OnboardingStatus.Failed
        };
    }
}
