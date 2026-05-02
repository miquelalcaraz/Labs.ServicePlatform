using Qlt.Services.Accounts.Domain.Models.CustomerActivationProcessAggregate;

namespace Qlt.Services.Accounts.Application.Queries;

public static class ActivationDisplayHelpers
{
    private static readonly List<ActivationStep> OrderedSteps =
    [
        ActivationStep.None,
        ActivationStep.ActivationRequested,
        ActivationStep.ActivationDelivered,
        ActivationStep.UserActivated,
        ActivationStep.CustomerReady,
        ActivationStep.Completed
    ];

    public static string GetStatusDisplayName(ActivationStatus status) => status switch
    {
        ActivationStatus.Draft => "Draft",
        ActivationStatus.InProgress => "In Progress",
        ActivationStatus.WaitingForNotificationDelivery => "Waiting for Email Delivery",
        ActivationStatus.WaitingForUserActivation => "Waiting for User Activation",
        ActivationStatus.Completed => "Completed",
        ActivationStatus.Failed => "Failed",
        _ => status.ToString()
    };

    public static string GetStepDisplayName(ActivationStep step) => step switch
    {
        ActivationStep.None => "Not Started",
        ActivationStep.ActivationRequested => "Activation Requested",
        ActivationStep.ActivationDelivered => "Activation Delivered",
        ActivationStep.UserActivated => "User Activated",
        ActivationStep.CustomerReady => "Customer Ready",
        ActivationStep.Completed => "Completed",
        _ => step.ToString()
    };

    public static int CalculateProgressPercent(ActivationStep step, ActivationStatus status)
    {
        if (status == ActivationStatus.Completed) return 100;

        return step switch
        {
            ActivationStep.None => 0,
            ActivationStep.ActivationRequested => 20,
            ActivationStep.ActivationDelivered => 45,
            ActivationStep.UserActivated => 70,
            ActivationStep.CustomerReady => 90,
            ActivationStep.Completed => 100,
            _ => 0
        };
    }

    public static IReadOnlyList<ActivationStepResponse> BuildStepsList(ActivationStep currentStep, ActivationStatus status)
    {
        var currentIndex = OrderedSteps.IndexOf(currentStep);
        var isFailed = status == ActivationStatus.Failed;

        return OrderedSteps.Skip(1).Select((step, i) =>
        {
            var stepIndex = i + 1;
            var isCompleted = status == ActivationStatus.Completed || stepIndex < currentIndex;
            var isCurrent = stepIndex == currentIndex && !isFailed;
            var isFailedStep = stepIndex == currentIndex && isFailed;

            return new ActivationStepResponse
            {
                Step = step.ToString(),
                DisplayName = GetStepDisplayName(step),
                IsCompleted = isCompleted,
                IsCurrent = isCurrent,
                IsFailed = isFailedStep
            };
        }).ToList();
    }

    public static ActivationDetailActionsResponse BuildDetailActions(ActivationStatus status) => new()
    {
        CanRetry = status == ActivationStatus.Failed,
        CanResend = status is ActivationStatus.WaitingForNotificationDelivery or ActivationStatus.WaitingForUserActivation,
        IsCompleted = status == ActivationStatus.Completed
    };

    public static ActivationListActionsResponse BuildListActions(ActivationStatus status) => new()
    {
        CanRetry = status == ActivationStatus.Failed,
        CanResend = status is ActivationStatus.WaitingForNotificationDelivery or ActivationStatus.WaitingForUserActivation
    };
}
