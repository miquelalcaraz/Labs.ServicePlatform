using Sirius.Services.Accounts.Domain.Events;
using Sirius.Services.Accounts.Domain.Exceptions;
using Sirius.Services.Accounts.Domain.Models.CustomerActivationProcessAggregate;

namespace Services.UnitTests.Accounts.Domain;

public class CustomerActivationProcessTests
{
    private static readonly Guid SubscriptionId = Guid.NewGuid();
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid OwnerCompanyId = Guid.NewGuid();
    private static readonly Guid AdminUserId = Guid.NewGuid();
    private static readonly Guid AdminMembershipId = Guid.NewGuid();

    private static CustomerActivationProcess CreateProcess() =>
        CustomerActivationProcess.Create(
            SubscriptionId, TenantId, OwnerCompanyId, AdminUserId, AdminMembershipId);

    private static CustomerActivationProcess CreateStartedProcess()
    {
        var process = CreateProcess();
        process.Start();
        return process;
    }

    private static CustomerActivationProcess AdvanceToActivationRequested()
    {
        var process = CreateStartedProcess();
        process.RequestActivation();
        return process;
    }

    private static CustomerActivationProcess AdvanceToActivationDelivered()
    {
        var process = AdvanceToActivationRequested();
        process.MarkActivationDelivered();
        return process;
    }

    private static CustomerActivationProcess AdvanceToUserActivated()
    {
        var process = AdvanceToActivationDelivered();
        process.MarkUserActivated(AdminUserId);
        return process;
    }

    private static CustomerActivationProcess AdvanceToCustomerReady()
    {
        var process = AdvanceToUserActivated();
        process.MarkCustomerReady();
        return process;
    }

    #region Create

    [Fact]
    public void Create_WithValidData_StartsAsDraftWithNoneStep()
    {
        var process = CustomerActivationProcess.Create(
            SubscriptionId, TenantId, OwnerCompanyId, AdminUserId, AdminMembershipId);

        process.SubscriptionId.Should().Be(SubscriptionId);
        process.TenantId.Should().Be(TenantId);
        process.OwnerCompanyId.Should().Be(OwnerCompanyId);
        process.AdminUserId.Should().Be(AdminUserId);
        process.AdminMembershipId.Should().Be(AdminMembershipId);
        process.Status.Should().Be(ActivationStatus.Draft);
        process.CurrentStep.Should().Be(ActivationStep.None);
        process.StartedAtUtc.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(5));
        process.RetryCount.Should().Be(0);
    }

    [Fact]
    public void Create_PublishesCustomerActivationProcessStartedDomainEvent()
    {
        var process = CreateProcess();

        process.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<CustomerActivationProcessStartedDomainEvent>()
            .Which.ProcessId.Should().Be(process.Id);
    }

    [Fact]
    public void Create_WithCorrelationId_StoresCorrelationId()
    {
        var correlationId = Guid.NewGuid();

        var process = CustomerActivationProcess.Create(
            SubscriptionId, TenantId, OwnerCompanyId, AdminUserId, AdminMembershipId,
            correlationId: correlationId);

        process.CorrelationId.Should().Be(correlationId);
    }

    [Fact]
    public void Create_WithEmptySubscriptionId_ThrowsAccountDomainException()
    {
        var act = () => CustomerActivationProcess.Create(
            Guid.Empty, TenantId, OwnerCompanyId, AdminUserId, AdminMembershipId);

        act.Should().Throw<AccountDomainException>();
    }

    [Fact]
    public void Create_WithEmptyTenantId_ThrowsAccountDomainException()
    {
        var act = () => CustomerActivationProcess.Create(
            SubscriptionId, Guid.Empty, OwnerCompanyId, AdminUserId, AdminMembershipId);

        act.Should().Throw<AccountDomainException>();
    }

    [Fact]
    public void Create_WithEmptyOwnerCompanyId_ThrowsAccountDomainException()
    {
        var act = () => CustomerActivationProcess.Create(
            SubscriptionId, TenantId, Guid.Empty, AdminUserId, AdminMembershipId);

        act.Should().Throw<AccountDomainException>();
    }

    [Fact]
    public void Create_WithEmptyAdminUserId_ThrowsAccountDomainException()
    {
        var act = () => CustomerActivationProcess.Create(
            SubscriptionId, TenantId, OwnerCompanyId, Guid.Empty, AdminMembershipId);

        act.Should().Throw<AccountDomainException>();
    }

    [Fact]
    public void Create_WithEmptyAdminMembershipId_ThrowsAccountDomainException()
    {
        var act = () => CustomerActivationProcess.Create(
            SubscriptionId, TenantId, OwnerCompanyId, AdminUserId, Guid.Empty);

        act.Should().Throw<AccountDomainException>();
    }

    #endregion

    #region Start

    [Fact]
    public void Start_FromDraft_SetsStatusToInProgress()
    {
        var process = CreateProcess();

        process.Start();

        process.Status.Should().Be(ActivationStatus.InProgress);
        process.CurrentStep.Should().Be(ActivationStep.None);
    }

    [Fact]
    public void Start_WhenNotDraft_ThrowsAccountDomainException()
    {
        var process = CreateStartedProcess();

        var act = () => process.Start();

        act.Should().Throw<AccountDomainException>();
    }

    #endregion

    #region RequestActivation

    [Fact]
    public void RequestActivation_FromInProgressNone_SetsWaitingForNotificationDelivery()
    {
        var process = CreateStartedProcess();

        process.RequestActivation();

        process.Status.Should().Be(ActivationStatus.WaitingForNotificationDelivery);
        process.CurrentStep.Should().Be(ActivationStep.ActivationRequested);
        process.ActivationRequestedAtUtc.Should().NotBeNull();
    }

    [Fact]
    public void RequestActivation_PublishesActivationEmailRequestedDomainEvent()
    {
        var process = CreateStartedProcess();
        process.ClearDomainEvents();

        process.RequestActivation();

        process.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<ActivationEmailRequestedDomainEvent>()
            .Which.Should().Match<ActivationEmailRequestedDomainEvent>(e =>
                e.ProcessId == process.Id &&
                e.UserId == AdminUserId &&
                e.TenantId == TenantId);
    }

    [Fact]
    public void RequestActivation_WhenNotInProgress_ThrowsAccountDomainException()
    {
        var process = CreateProcess();

        var act = () => process.RequestActivation();

        act.Should().Throw<AccountDomainException>();
    }

    [Fact]
    public void RequestActivation_WhenStepNotNone_ThrowsAccountDomainException()
    {
        var process = AdvanceToActivationRequested();
        process.MarkActivationDelivered();
        process.MarkUserActivated(AdminUserId);

        var act = () => process.RequestActivation();

        act.Should().Throw<AccountDomainException>();
    }

    #endregion

    #region MarkActivationDelivered

    [Fact]
    public void MarkActivationDelivered_FromActivationRequested_SetsWaitingForUserActivation()
    {
        var process = AdvanceToActivationRequested();

        process.MarkActivationDelivered();

        process.Status.Should().Be(ActivationStatus.WaitingForUserActivation);
        process.CurrentStep.Should().Be(ActivationStep.ActivationDelivered);
        process.ActivationDeliveredAtUtc.Should().NotBeNull();
    }

    [Fact]
    public void MarkActivationDelivered_WhenNotWaitingForNotificationDelivery_ThrowsAccountDomainException()
    {
        var process = CreateStartedProcess();

        var act = () => process.MarkActivationDelivered();

        act.Should().Throw<AccountDomainException>();
    }

    #endregion

    #region MarkUserActivated

    [Fact]
    public void MarkUserActivated_WithCorrectUserId_SetsInProgressAndUserActivatedStep()
    {
        var process = AdvanceToActivationDelivered();

        process.MarkUserActivated(AdminUserId);

        process.Status.Should().Be(ActivationStatus.InProgress);
        process.CurrentStep.Should().Be(ActivationStep.UserActivated);
        process.UserActivatedAtUtc.Should().NotBeNull();
    }

    [Fact]
    public void MarkUserActivated_PublishesCustomerAdminActivatedDomainEvent()
    {
        var process = AdvanceToActivationDelivered();
        process.ClearDomainEvents();

        process.MarkUserActivated(AdminUserId);

        process.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<CustomerAdminActivatedDomainEvent>()
            .Which.Should().Match<CustomerAdminActivatedDomainEvent>(e =>
                e.ProcessId == process.Id &&
                e.UserId == AdminUserId);
    }

    [Fact]
    public void MarkUserActivated_WithWrongUserId_ThrowsAccountDomainException()
    {
        var process = AdvanceToActivationDelivered();

        var act = () => process.MarkUserActivated(Guid.NewGuid());

        act.Should().Throw<AccountDomainException>();
    }

    [Fact]
    public void MarkUserActivated_WhenNotWaitingForUserActivation_ThrowsAccountDomainException()
    {
        var process = CreateStartedProcess();

        var act = () => process.MarkUserActivated(AdminUserId);

        act.Should().Throw<AccountDomainException>();
    }

    #endregion

    #region MarkCustomerReady

    [Fact]
    public void MarkCustomerReady_FromUserActivated_SetsCustomerReadyStep()
    {
        var process = AdvanceToUserActivated();

        process.MarkCustomerReady();

        process.Status.Should().Be(ActivationStatus.InProgress);
        process.CurrentStep.Should().Be(ActivationStep.CustomerReady);
    }

    [Fact]
    public void MarkCustomerReady_WhenStepNotUserActivated_ThrowsAccountDomainException()
    {
        var process = CreateStartedProcess();

        var act = () => process.MarkCustomerReady();

        act.Should().Throw<AccountDomainException>();
    }

    #endregion

    #region Complete

    [Fact]
    public void Complete_FromCustomerReady_SetsCompletedStatusAndStep()
    {
        var process = AdvanceToCustomerReady();

        process.Complete();

        process.Status.Should().Be(ActivationStatus.Completed);
        process.CurrentStep.Should().Be(ActivationStep.Completed);
        process.CompletedAtUtc.Should().NotBeNull();
    }

    [Fact]
    public void Complete_PublishesCustomerGoLiveDomainEvent()
    {
        var process = AdvanceToCustomerReady();
        process.ClearDomainEvents();

        process.Complete();

        process.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<CustomerGoLiveDomainEvent>()
            .Which.Should().Match<CustomerGoLiveDomainEvent>(e =>
                e.SubscriptionId == SubscriptionId &&
                e.TenantId == TenantId &&
                e.AdminUserId == AdminUserId);
    }

    [Fact]
    public void Complete_WhenNotCustomerReadyStep_ThrowsAccountDomainException()
    {
        var process = AdvanceToUserActivated();

        var act = () => process.Complete();

        act.Should().Throw<AccountDomainException>();
    }

    [Fact]
    public void Complete_WhenNotInProgress_ThrowsAccountDomainException()
    {
        var process = AdvanceToActivationRequested();

        var act = () => process.Complete();

        act.Should().Throw<AccountDomainException>();
    }

    #endregion

    #region Fail

    [Fact]
    public void Fail_SetsStatusToFailedAndRecordsError()
    {
        var process = CreateStartedProcess();

        process.Fail("ERR001", "Something went wrong");

        process.Status.Should().Be(ActivationStatus.Failed);
        process.LastErrorCode.Should().Be("ERR001");
        process.LastErrorMessage.Should().Be("Something went wrong");
    }

    [Fact]
    public void Fail_WhenCompleted_ThrowsAccountDomainException()
    {
        var process = AdvanceToCustomerReady();
        process.Complete();

        var act = () => process.Fail("ERR001", "Cannot fail after completion");

        act.Should().Throw<AccountDomainException>();
    }

    [Fact]
    public void Fail_WhenDraft_SetsStatusToFailed()
    {
        var process = CreateProcess();

        process.Fail("ERR001", "Failed during draft");

        process.Status.Should().Be(ActivationStatus.Failed);
    }

    [Fact]
    public void Fail_WhenWaitingForNotificationDelivery_SetsStatusToFailed()
    {
        var process = AdvanceToActivationRequested();

        process.Fail("ERR001", "Notification delivery failed");

        process.Status.Should().Be(ActivationStatus.Failed);
    }

    #endregion

    #region Retry

    [Fact]
    public void Retry_WhenFailed_ResetsToInProgressAndIncrementsRetryCount()
    {
        var process = CreateStartedProcess();
        process.Fail("ERR001", "Something failed");

        process.Retry();

        process.Status.Should().Be(ActivationStatus.InProgress);
        process.LastErrorCode.Should().BeNull();
        process.LastErrorMessage.Should().BeNull();
        process.RetryCount.Should().Be(1);
    }

    [Fact]
    public void Retry_PreservesCurrentStep()
    {
        var process = AdvanceToActivationRequested();
        process.Fail("ERR001", "Delivery failed");

        process.Retry();

        process.CurrentStep.Should().Be(ActivationStep.ActivationRequested);
    }

    [Fact]
    public void Retry_MultipleTimes_IncrementsRetryCount()
    {
        var process = CreateStartedProcess();

        process.Fail("ERR001", "First failure");
        process.Retry();
        process.Fail("ERR002", "Second failure");
        process.Retry();

        process.RetryCount.Should().Be(2);
    }

    [Fact]
    public void Retry_WhenNotFailed_ThrowsAccountDomainException()
    {
        var process = CreateStartedProcess();

        var act = () => process.Retry();

        act.Should().Throw<AccountDomainException>();
    }

    [Fact]
    public void Retry_WhenCompleted_ThrowsAccountDomainException()
    {
        var process = AdvanceToCustomerReady();
        process.Complete();

        var act = () => process.Retry();

        act.Should().Throw<AccountDomainException>();
    }

    #endregion

    #region Full Happy Path

    [Fact]
    public void CanAdvanceThroughFullHappyPath()
    {
        var process = CustomerActivationProcess.Create(
            SubscriptionId, TenantId, OwnerCompanyId, AdminUserId, AdminMembershipId);

        process.Start();
        process.RequestActivation();
        process.MarkActivationDelivered();
        process.MarkUserActivated(AdminUserId);
        process.MarkCustomerReady();
        process.Complete();

        process.Status.Should().Be(ActivationStatus.Completed);
        process.CurrentStep.Should().Be(ActivationStep.Completed);
        process.CompletedAtUtc.Should().NotBeNull();
        process.ActivationRequestedAtUtc.Should().NotBeNull();
        process.ActivationDeliveredAtUtc.Should().NotBeNull();
        process.UserActivatedAtUtc.Should().NotBeNull();
    }

    [Fact]
    public void Retry_ThenReAdvance_ContinuesFromFailedStep()
    {
        var process = AdvanceToActivationRequested();
        process.Fail("ERR001", "Notification service down");

        process.Retry();

        process.Status.Should().Be(ActivationStatus.InProgress);
        process.CurrentStep.Should().Be(ActivationStep.ActivationRequested);
        process.RetryCount.Should().Be(1);
    }

    #endregion

    #region Invalid Transitions from Terminal States

    [Fact]
    public void Start_WhenCompleted_ThrowsAccountDomainException()
    {
        var process = AdvanceToCustomerReady();
        process.Complete();

        var act = () => process.Start();

        act.Should().Throw<AccountDomainException>();
    }

    [Fact]
    public void Start_WhenFailed_ThrowsAccountDomainException()
    {
        var process = CreateStartedProcess();
        process.Fail("ERR001", "Something failed");

        var act = () => process.Start();

        act.Should().Throw<AccountDomainException>();
    }

    [Fact]
    public void RequestActivation_WhenCompleted_ThrowsAccountDomainException()
    {
        var process = AdvanceToCustomerReady();
        process.Complete();

        var act = () => process.RequestActivation();

        act.Should().Throw<AccountDomainException>();
    }

    #endregion
}
