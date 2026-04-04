using Qlt.Services.Accounts.Domain.Exceptions;
using Qlt.Services.Accounts.Domain.Models.OnboardingAggregate;

namespace Services.UnitTests.Accounts.Domain;

public class OnboardingProcessTests
{
    private static OnboardingProcess CreateOnboarding() =>
        OnboardingProcess.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());

    private static OnboardingProcess CreateStartedOnboarding()
    {
        var onboarding = CreateOnboarding();
        onboarding.Start();
        return onboarding;
    }

    private static OnboardingProcess AdvanceToAdminProvisioningRequested()
    {
        var onboarding = CreateStartedOnboarding();
        onboarding.MarkSubscriptionCreated();
        onboarding.MarkTenantCreated();
        onboarding.MarkOwnerCompanyCreated(Guid.NewGuid());
        onboarding.MarkAdminProvisioningRequested();
        return onboarding;
    }

    private static OnboardingProcess AdvanceToAdminMembershipCreated()
    {
        var onboarding = AdvanceToAdminProvisioningRequested();
        onboarding.MarkAdminUserProvisioned(Guid.NewGuid());
        onboarding.MarkAdminMembershipCreated();
        return onboarding;
    }

    [Fact]
    public void Create_WithValidData_StartsAsDraftWithNoneStep()
    {
        var subscriptionId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var internalUserId = Guid.NewGuid();

        var onboarding = OnboardingProcess.Create(subscriptionId, tenantId, internalUserId);

        onboarding.SubscriptionId.Should().Be(subscriptionId);
        onboarding.TenantId.Should().Be(tenantId);
        onboarding.InitiatedByInternalUserId.Should().Be(internalUserId);
        onboarding.Status.Should().Be(OnboardingStatus.Draft);
        onboarding.CurrentStep.Should().Be(OnboardingStep.None);
        onboarding.StartedAtUtc.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void Create_WithEmptySubscriptionId_ThrowsAccountDomainException()
    {
        var act = () => OnboardingProcess.Create(Guid.Empty, Guid.NewGuid(), Guid.NewGuid());

        act.Should().Throw<AccountDomainException>();
    }

    [Fact]
    public void Create_WithEmptyTenantId_ThrowsAccountDomainException()
    {
        var act = () => OnboardingProcess.Create(Guid.NewGuid(), Guid.Empty, Guid.NewGuid());

        act.Should().Throw<AccountDomainException>();
    }

    [Fact]
    public void Create_WithEmptyInitiatedByInternalUserId_ThrowsAccountDomainException()
    {
        var act = () => OnboardingProcess.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.Empty);

        act.Should().Throw<AccountDomainException>();
    }

    [Fact]
    public void Start_FromDraft_SetsStatusToInProgress()
    {
        var onboarding = CreateOnboarding();

        onboarding.Start();

        onboarding.Status.Should().Be(OnboardingStatus.InProgress);
        onboarding.CurrentStep.Should().Be(OnboardingStep.None);
    }

    [Fact]
    public void Start_WhenNotDraft_ThrowsAccountDomainException()
    {
        var onboarding = CreateStartedOnboarding();

        var act = () => onboarding.Start();

        act.Should().Throw<AccountDomainException>();
    }

    [Fact]
    public void MarkSubscriptionCreated_FromInProgressNone_AdvancesStep()
    {
        var onboarding = CreateStartedOnboarding();

        onboarding.MarkSubscriptionCreated();

        onboarding.Status.Should().Be(OnboardingStatus.InProgress);
        onboarding.CurrentStep.Should().Be(OnboardingStep.SubscriptionCreated);
    }

    [Fact]
    public void MarkSubscriptionCreated_WhenStepNotNone_ThrowsAccountDomainException()
    {
        var onboarding = CreateStartedOnboarding();
        onboarding.MarkSubscriptionCreated();

        var act = () => onboarding.MarkSubscriptionCreated();

        act.Should().Throw<AccountDomainException>();
    }

    [Fact]
    public void MarkTenantCreated_FromSubscriptionCreated_AdvancesStep()
    {
        var onboarding = CreateStartedOnboarding();
        onboarding.MarkSubscriptionCreated();

        onboarding.MarkTenantCreated();

        onboarding.CurrentStep.Should().Be(OnboardingStep.TenantCreated);
    }

    [Fact]
    public void MarkTenantCreated_WhenStepNotSubscriptionCreated_ThrowsAccountDomainException()
    {
        var onboarding = CreateStartedOnboarding();

        var act = () => onboarding.MarkTenantCreated();

        act.Should().Throw<AccountDomainException>();
    }

    [Fact]
    public void MarkOwnerCompanyCreated_FromTenantCreated_SetsCompanyIdAndAdvancesStep()
    {
        var onboarding = CreateStartedOnboarding();
        onboarding.MarkSubscriptionCreated();
        onboarding.MarkTenantCreated();
        var companyId = Guid.NewGuid();

        onboarding.MarkOwnerCompanyCreated(companyId);

        onboarding.OwnerCompanyId.Should().Be(companyId);
        onboarding.CurrentStep.Should().Be(OnboardingStep.OwnerCompanyCreated);
    }

    [Fact]
    public void MarkOwnerCompanyCreated_WithEmptyCompanyId_ThrowsAccountDomainException()
    {
        var onboarding = CreateStartedOnboarding();
        onboarding.MarkSubscriptionCreated();
        onboarding.MarkTenantCreated();

        var act = () => onboarding.MarkOwnerCompanyCreated(Guid.Empty);

        act.Should().Throw<AccountDomainException>();
    }

    [Fact]
    public void MarkAdminProvisioningRequested_SetsStatusToWaitingForExternalProvisioning()
    {
        var onboarding = CreateStartedOnboarding();
        onboarding.MarkSubscriptionCreated();
        onboarding.MarkTenantCreated();
        onboarding.MarkOwnerCompanyCreated(Guid.NewGuid());

        onboarding.MarkAdminProvisioningRequested();

        onboarding.Status.Should().Be(OnboardingStatus.WaitingForExternalProvisioning);
        onboarding.CurrentStep.Should().Be(OnboardingStep.AdminProvisioningRequested);
    }

    [Fact]
    public void MarkAdminUserProvisioned_SetsAdminUserIdAndReturnsToInProgress()
    {
        var onboarding = AdvanceToAdminProvisioningRequested();
        var userId = Guid.NewGuid();

        onboarding.MarkAdminUserProvisioned(userId);

        onboarding.AdminUserId.Should().Be(userId);
        onboarding.Status.Should().Be(OnboardingStatus.InProgress);
        onboarding.CurrentStep.Should().Be(OnboardingStep.AdminUserProvisioned);
    }

    [Fact]
    public void MarkAdminUserProvisioned_WithEmptyUserId_ThrowsAccountDomainException()
    {
        var onboarding = AdvanceToAdminProvisioningRequested();

        var act = () => onboarding.MarkAdminUserProvisioned(Guid.Empty);

        act.Should().Throw<AccountDomainException>();
    }

    [Fact]
    public void MarkAdminUserProvisioned_WhenNotWaiting_ThrowsAccountDomainException()
    {
        var onboarding = CreateStartedOnboarding();

        var act = () => onboarding.MarkAdminUserProvisioned(Guid.NewGuid());

        act.Should().Throw<AccountDomainException>();
    }

    [Fact]
    public void MarkAdminMembershipCreated_FromAdminUserProvisioned_AdvancesStep()
    {
        var onboarding = AdvanceToAdminProvisioningRequested();
        onboarding.MarkAdminUserProvisioned(Guid.NewGuid());

        onboarding.MarkAdminMembershipCreated();

        onboarding.CurrentStep.Should().Be(OnboardingStep.AdminMembershipCreated);
        onboarding.Status.Should().Be(OnboardingStatus.InProgress);
    }

    [Fact]
    public void MarkActivationRequested_SetsStatusToWaitingForActivationDelivery()
    {
        var onboarding = AdvanceToAdminMembershipCreated();

        onboarding.MarkActivationRequested();

        onboarding.Status.Should().Be(OnboardingStatus.WaitingForActivationDelivery);
        onboarding.CurrentStep.Should().Be(OnboardingStep.ActivationRequested);
    }

    [Fact]
    public void MarkActivationDelivered_SetsStatusToWaitingForUserActivation()
    {
        var onboarding = AdvanceToAdminMembershipCreated();
        onboarding.MarkActivationRequested();

        onboarding.MarkActivationDelivered();

        onboarding.Status.Should().Be(OnboardingStatus.WaitingForUserActivation);
        onboarding.CurrentStep.Should().Be(OnboardingStep.ActivationDelivered);
        onboarding.ActivationDeliveredAtUtc.Should().NotBeNull();
    }

    [Fact]
    public void MarkUserActivated_CompletesOnboarding()
    {
        var onboarding = AdvanceToAdminMembershipCreated();
        onboarding.MarkActivationRequested();
        onboarding.MarkActivationDelivered();

        onboarding.MarkUserActivated();

        onboarding.Status.Should().Be(OnboardingStatus.Completed);
        onboarding.CurrentStep.Should().Be(OnboardingStep.Completed);
        onboarding.UserActivatedAtUtc.Should().NotBeNull();
        onboarding.CompletedAtUtc.Should().NotBeNull();
    }

    [Fact]
    public void CanAdvanceThroughFullHappyPath()
    {
        var onboarding = CreateOnboarding();
        var companyId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        onboarding.Start();
        onboarding.MarkSubscriptionCreated();
        onboarding.MarkTenantCreated();
        onboarding.MarkOwnerCompanyCreated(companyId);
        onboarding.MarkAdminProvisioningRequested();
        onboarding.MarkAdminUserProvisioned(userId);
        onboarding.MarkAdminMembershipCreated();
        onboarding.MarkActivationRequested();
        onboarding.MarkActivationDelivered();
        onboarding.MarkUserActivated();

        onboarding.Status.Should().Be(OnboardingStatus.Completed);
        onboarding.CurrentStep.Should().Be(OnboardingStep.Completed);
        onboarding.OwnerCompanyId.Should().Be(companyId);
        onboarding.AdminUserId.Should().Be(userId);
        onboarding.CompletedAtUtc.Should().NotBeNull();
    }

    [Fact]
    public void Complete_WhenMinimumDataPresent_SetsStatusToCompleted()
    {
        var onboarding = AdvanceToAdminMembershipCreated();

        onboarding.Complete();

        onboarding.Status.Should().Be(OnboardingStatus.Completed);
        onboarding.CurrentStep.Should().Be(OnboardingStep.Completed);
        onboarding.CompletedAtUtc.Should().NotBeNull();
    }

    [Fact]
    public void Complete_WithoutOwnerCompany_ThrowsAccountDomainException()
    {
        var onboarding = CreateStartedOnboarding();

        var act = () => onboarding.Complete();

        act.Should().Throw<AccountDomainException>();
    }

    [Fact]
    public void Complete_WithoutAdminUser_ThrowsAccountDomainException()
    {
        var onboarding = CreateStartedOnboarding();
        onboarding.MarkSubscriptionCreated();
        onboarding.MarkTenantCreated();
        onboarding.MarkOwnerCompanyCreated(Guid.NewGuid());

        var act = () => onboarding.Complete();

        act.Should().Throw<AccountDomainException>();
    }

    [Fact]
    public void Fail_SetsStatusToFailedAndRecordsFailedStep()
    {
        var onboarding = CreateStartedOnboarding();
        onboarding.MarkSubscriptionCreated();

        onboarding.Fail("ERR001", "Provisioning failed");

        onboarding.Status.Should().Be(OnboardingStatus.Failed);
        onboarding.ErrorCode.Should().Be("ERR001");
        onboarding.ErrorMessage.Should().Be("Provisioning failed");
        onboarding.FailedStep.Should().Be(OnboardingStep.SubscriptionCreated);
        onboarding.FailedAtUtc.Should().NotBeNull();
    }

    [Fact]
    public void Fail_WhenCompleted_ThrowsAccountDomainException()
    {
        var onboarding = AdvanceToAdminMembershipCreated();
        onboarding.Complete();

        var act = () => onboarding.Fail("ERR001", "Cannot fail after completion");

        act.Should().Throw<AccountDomainException>();
    }

    [Fact]
    public void Retry_WhenFailed_ResetsToInProgressAndIncrementsRetryCount()
    {
        var onboarding = AdvanceToAdminProvisioningRequested();
        onboarding.Fail("ERR001", "Provisioning failed");

        onboarding.Retry();

        onboarding.Status.Should().Be(OnboardingStatus.InProgress);
        onboarding.ErrorCode.Should().BeNull();
        onboarding.ErrorMessage.Should().BeNull();
        onboarding.FailedAtUtc.Should().BeNull();
        onboarding.RetryCount.Should().Be(1);
        onboarding.LastRetryAtUtc.Should().NotBeNull();
    }

    [Fact]
    public void Retry_MultipleTimes_IncrementsRetryCount()
    {
        var onboarding = CreateStartedOnboarding();

        onboarding.Fail("ERR001", "First failure");
        onboarding.Retry();
        onboarding.Fail("ERR002", "Second failure");
        onboarding.Retry();

        onboarding.RetryCount.Should().Be(2);
    }

    [Fact]
    public void Retry_WhenNotFailed_ThrowsAccountDomainException()
    {
        var onboarding = CreateStartedOnboarding();

        var act = () => onboarding.Retry();

        act.Should().Throw<AccountDomainException>();
    }

    [Fact]
    public void Retry_PreservesCurrentStep()
    {
        var onboarding = AdvanceToAdminProvisioningRequested();
        onboarding.Fail("ERR001", "Provisioning failed");

        onboarding.Retry();

        onboarding.CurrentStep.Should().Be(OnboardingStep.AdminProvisioningRequested);
    }

    [Fact]
    public void Start_WhenCompleted_ThrowsAccountDomainException()
    {
        var onboarding = AdvanceToAdminMembershipCreated();
        onboarding.Complete();

        var act = () => onboarding.Start();

        act.Should().Throw<AccountDomainException>();
    }

    [Fact]
    public void MarkSubscriptionCreated_WhenCompleted_ThrowsAccountDomainException()
    {
        var onboarding = AdvanceToAdminMembershipCreated();
        onboarding.Complete();

        var act = () => onboarding.MarkSubscriptionCreated();

        act.Should().Throw<AccountDomainException>();
    }

    [Fact]
    public void Retry_WhenCompleted_ThrowsAccountDomainException()
    {
        var onboarding = AdvanceToAdminMembershipCreated();
        onboarding.Complete();

        var act = () => onboarding.Retry();

        act.Should().Throw<AccountDomainException>();
    }

    [Fact]
    public void Start_WhenFailed_ThrowsAccountDomainException()
    {
        var onboarding = CreateStartedOnboarding();
        onboarding.Fail("ERR001", "Something failed");

        var act = () => onboarding.Start();

        act.Should().Throw<AccountDomainException>();
    }

    [Fact]
    public void MarkSubscriptionCreated_WhenFailed_ThrowsAccountDomainException()
    {
        var onboarding = CreateStartedOnboarding();
        onboarding.Fail("ERR001", "Something failed");

        var act = () => onboarding.MarkSubscriptionCreated();

        act.Should().Throw<AccountDomainException>();
    }

    [Fact]
    public void MarkSubscriptionCreated_WhenDraft_ThrowsAccountDomainException()
    {
        var onboarding = CreateOnboarding();

        var act = () => onboarding.MarkSubscriptionCreated();

        act.Should().Throw<AccountDomainException>();
    }

    [Fact]
    public void MarkOwnerCompanyCreated_WhenStepNotTenantCreated_ThrowsAccountDomainException()
    {
        var onboarding = CreateStartedOnboarding();
        onboarding.MarkSubscriptionCreated();

        var act = () => onboarding.MarkOwnerCompanyCreated(Guid.NewGuid());

        act.Should().Throw<AccountDomainException>();
    }

    [Fact]
    public void MarkAdminProvisioningRequested_WhenStepNotOwnerCompanyCreated_ThrowsAccountDomainException()
    {
        var onboarding = CreateStartedOnboarding();
        onboarding.MarkSubscriptionCreated();
        onboarding.MarkTenantCreated();

        var act = () => onboarding.MarkAdminProvisioningRequested();

        act.Should().Throw<AccountDomainException>();
    }

    [Fact]
    public void MarkAdminMembershipCreated_WhenStepNotAdminUserProvisioned_ThrowsAccountDomainException()
    {
        var onboarding = AdvanceToAdminProvisioningRequested();

        var act = () => onboarding.MarkAdminMembershipCreated();

        act.Should().Throw<AccountDomainException>();
    }

    [Fact]
    public void MarkActivationRequested_WhenStepNotAdminMembershipCreated_ThrowsAccountDomainException()
    {
        var onboarding = AdvanceToAdminProvisioningRequested();
        onboarding.MarkAdminUserProvisioned(Guid.NewGuid());

        var act = () => onboarding.MarkActivationRequested();

        act.Should().Throw<AccountDomainException>();
    }

    [Fact]
    public void MarkActivationDelivered_WhenNotWaitingForActivationDelivery_ThrowsAccountDomainException()
    {
        var onboarding = AdvanceToAdminMembershipCreated();

        var act = () => onboarding.MarkActivationDelivered();

        act.Should().Throw<AccountDomainException>();
    }

    [Fact]
    public void MarkUserActivated_WhenNotWaitingForUserActivation_ThrowsAccountDomainException()
    {
        var onboarding = AdvanceToAdminMembershipCreated();
        onboarding.MarkActivationRequested();

        var act = () => onboarding.MarkUserActivated();

        act.Should().Throw<AccountDomainException>();
    }

    [Fact]
    public void Fail_WhenDraft_RecordsNoneAsFailedStep()
    {
        var onboarding = CreateOnboarding();

        onboarding.Fail("ERR001", "Failed during draft");

        onboarding.Status.Should().Be(OnboardingStatus.Failed);
        onboarding.FailedStep.Should().Be(OnboardingStep.None);
    }

    [Fact]
    public void Fail_WhenWaitingForExternalProvisioning_RecordsCurrentStep()
    {
        var onboarding = AdvanceToAdminProvisioningRequested();

        onboarding.Fail("ERR001", "External provisioning failed");

        onboarding.Status.Should().Be(OnboardingStatus.Failed);
        onboarding.FailedStep.Should().Be(OnboardingStep.AdminProvisioningRequested);
    }

    [Fact]
    public void Retry_ThenReAdvance_ContinuesFromCurrentStep()
    {
        var onboarding = CreateStartedOnboarding();
        onboarding.MarkSubscriptionCreated();
        onboarding.Fail("ERR001", "Tenant creation failed");

        onboarding.Retry();
        onboarding.MarkTenantCreated();

        onboarding.Status.Should().Be(OnboardingStatus.InProgress);
        onboarding.CurrentStep.Should().Be(OnboardingStep.TenantCreated);
        onboarding.RetryCount.Should().Be(1);
    }
}
