using Sirius.Services.Accounts.Application.Queries;
using Sirius.Services.Accounts.Domain.Models.OnboardingAggregate;

namespace Services.UnitTests.Accounts.Application.Queries;

public class OnboardingDisplayHelpersTests
{
    [Theory]
    [InlineData(OnboardingStatus.Draft, "Draft")]
    [InlineData(OnboardingStatus.InProgress, "In Progress")]
    [InlineData(OnboardingStatus.WaitingForExternalProvisioning, "Waiting for Identity Provisioning")]
    [InlineData(OnboardingStatus.Completed, "Completed")]
    [InlineData(OnboardingStatus.Failed, "Failed")]
    public void GetStatusDisplayName_ReturnsExpectedName(OnboardingStatus status, string expected)
    {
        OnboardingDisplayHelpers.GetStatusDisplayName(status).Should().Be(expected);
    }

    [Theory]
    [InlineData(OnboardingStep.None, "Not Started")]
    [InlineData(OnboardingStep.SubscriptionCreated, "Subscription Created")]
    [InlineData(OnboardingStep.TenantCreated, "Tenant Created")]
    [InlineData(OnboardingStep.OwnerCompanyCreated, "Owner Company Created")]
    [InlineData(OnboardingStep.AdminProvisioningRequested, "Admin Provisioning Requested")]
    [InlineData(OnboardingStep.AdminUserProvisioned, "Admin User Provisioned")]
    [InlineData(OnboardingStep.AdminMembershipCreated, "Admin Membership Created")]
    [InlineData(OnboardingStep.Completed, "Completed")]
    public void GetStepDisplayName_ReturnsExpectedName(OnboardingStep step, string expected)
    {
        OnboardingDisplayHelpers.GetStepDisplayName(step).Should().Be(expected);
    }

    [Fact]
    public void CalculateProgressPercent_WhenCompleted_Returns100()
    {
        var result = OnboardingDisplayHelpers.CalculateProgressPercent(OnboardingStep.Completed, OnboardingStatus.Completed);
        result.Should().Be(100);
    }

    [Fact]
    public void CalculateProgressPercent_WhenNone_Returns0()
    {
        var result = OnboardingDisplayHelpers.CalculateProgressPercent(OnboardingStep.None, OnboardingStatus.Draft);
        result.Should().Be(0);
    }

    [Fact]
    public void CalculateProgressPercent_WhenAdminProvisioningRequested_Returns55()
    {
        var result = OnboardingDisplayHelpers.CalculateProgressPercent(
            OnboardingStep.AdminProvisioningRequested,
            OnboardingStatus.WaitingForExternalProvisioning);
        result.Should().Be(55);
    }

    [Fact]
    public void CalculateProgressPercent_CompletedStatus_AlwaysReturns100_RegardlessOfStep()
    {
        var result = OnboardingDisplayHelpers.CalculateProgressPercent(
            OnboardingStep.AdminMembershipCreated,
            OnboardingStatus.Completed);
        result.Should().Be(100);
    }

    [Fact]
    public void BuildStepsList_Returns7Steps()
    {
        var steps = OnboardingDisplayHelpers.BuildStepsList(
            OnboardingStep.OwnerCompanyCreated,
            OnboardingStatus.InProgress,
            null);

        steps.Should().HaveCount(7);
    }

    [Fact]
    public void BuildStepsList_MarksPriorStepsAsCompleted()
    {
        var steps = OnboardingDisplayHelpers.BuildStepsList(
            OnboardingStep.OwnerCompanyCreated,
            OnboardingStatus.InProgress,
            null);

        steps[0].Status.Should().Be("Completed");
        steps[1].Status.Should().Be("Completed");
        steps[2].Status.Should().Be("Current");
        steps[3].Status.Should().Be("Pending");
    }

    [Fact]
    public void BuildStepsList_WhenFailed_MarksFailedStepAsFailed()
    {
        var steps = OnboardingDisplayHelpers.BuildStepsList(
            OnboardingStep.AdminProvisioningRequested,
            OnboardingStatus.Failed,
            OnboardingStep.AdminProvisioningRequested);

        steps[3].Code.Should().Be("AdminProvisioningRequested");
        steps[3].Status.Should().Be("Failed");
    }

    [Fact]
    public void BuildStepsList_WhenCompleted_AllStepsExceptLastAreCompleted()
    {
        var steps = OnboardingDisplayHelpers.BuildStepsList(
            OnboardingStep.Completed,
            OnboardingStatus.Completed,
            null);

        steps.SkipLast(1).Should().AllSatisfy(s => s.Status.Should().Be("Completed"));
        steps.Last().Status.Should().Be("Completed");
    }

    [Fact]
    public void BuildDetailActions_WhenFailed_CanRetryIsTrue()
    {
        var actions = OnboardingDisplayHelpers.BuildDetailActions(OnboardingStatus.Failed);

        actions.CanRetry.Should().BeTrue();
        actions.CanRefresh.Should().BeFalse();
        actions.CanOpenCustomer.Should().BeFalse();
    }

    [Fact]
    public void BuildDetailActions_WhenInProgress_CanRefreshIsTrue()
    {
        var actions = OnboardingDisplayHelpers.BuildDetailActions(OnboardingStatus.InProgress);

        actions.CanRetry.Should().BeFalse();
        actions.CanRefresh.Should().BeTrue();
        actions.CanOpenCustomer.Should().BeFalse();
    }

    [Fact]
    public void BuildDetailActions_WhenCompleted_CanOpenCustomerIsTrue()
    {
        var actions = OnboardingDisplayHelpers.BuildDetailActions(OnboardingStatus.Completed);

        actions.CanRetry.Should().BeFalse();
        actions.CanRefresh.Should().BeFalse();
        actions.CanOpenCustomer.Should().BeTrue();
    }

    [Fact]
    public void BuildListActions_WhenFailed_CanRetryIsTrue()
    {
        var actions = OnboardingDisplayHelpers.BuildListActions(OnboardingStatus.Failed);

        actions.CanOpen.Should().BeTrue();
        actions.CanRetry.Should().BeTrue();
    }

    [Fact]
    public void BuildListActions_WhenInProgress_CanRetryIsFalse()
    {
        var actions = OnboardingDisplayHelpers.BuildListActions(OnboardingStatus.InProgress);

        actions.CanOpen.Should().BeTrue();
        actions.CanRetry.Should().BeFalse();
    }

    [Theory]
    [InlineData(OnboardingStep.SubscriptionCreated, 15)]
    [InlineData(OnboardingStep.TenantCreated, 30)]
    [InlineData(OnboardingStep.OwnerCompanyCreated, 45)]
    [InlineData(OnboardingStep.AdminUserProvisioned, 70)]
    [InlineData(OnboardingStep.AdminMembershipCreated, 85)]
    public void CalculateProgressPercent_AllSteps_ReturnExpectedValues(OnboardingStep step, int expectedPercent)
    {
        var result = OnboardingDisplayHelpers.CalculateProgressPercent(step, OnboardingStatus.InProgress);
        result.Should().Be(expectedPercent);
    }
}
