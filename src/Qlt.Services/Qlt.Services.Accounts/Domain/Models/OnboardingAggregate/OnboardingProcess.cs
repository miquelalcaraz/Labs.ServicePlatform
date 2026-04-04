using Qlt.Domain.SeedWork;
using Qlt.Services.Accounts.Domain.Exceptions;

namespace Qlt.Services.Accounts.Domain.Models.OnboardingAggregate;

public class OnboardingProcess : Entity, IAggregateRoot
{
    public Guid SubscriptionId { get; private set; }

    public Guid TenantId { get; private set; }

    public Guid? OwnerCompanyId { get; private set; }

    public Guid? AdminUserId { get; private set; }

    public OnboardingStatus Status { get; private set; }

    public OnboardingStep CurrentStep { get; private set; }

    public string? ErrorCode { get; private set; }

    public string? ErrorMessage { get; private set; }

    public DateTimeOffset StartedAtUtc { get; private set; }

    public DateTimeOffset? CompletedAtUtc { get; private set; }

    public DateTimeOffset? FailedAtUtc { get; private set; }

    public Guid InitiatedByInternalUserId { get; private set; }

    public Guid? CorrelationId { get; private set; }

    public int RetryCount { get; private set; }

    public DateTimeOffset? LastRetryAtUtc { get; private set; }

    public OnboardingStep? FailedStep { get; private set; }

    public DateTimeOffset? ActivationDeliveredAtUtc { get; private set; }

    public DateTimeOffset? UserActivatedAtUtc { get; private set; }

    protected OnboardingProcess() { }

    public static OnboardingProcess Create(
        Guid subscriptionId,
        Guid tenantId,
        Guid initiatedByInternalUserId)
    {
        if (subscriptionId == Guid.Empty)
            throw new AccountDomainException("SubscriptionId is required.");

        if (tenantId == Guid.Empty)
            throw new AccountDomainException("TenantId is required.");

        if (initiatedByInternalUserId == Guid.Empty)
            throw new AccountDomainException("InitiatedByInternalUserId is required.");

        return new OnboardingProcess
        {
            SubscriptionId = subscriptionId,
            TenantId = tenantId,
            Status = OnboardingStatus.Draft,
            CurrentStep = OnboardingStep.None,
            StartedAtUtc = DateTimeOffset.UtcNow,
            InitiatedByInternalUserId = initiatedByInternalUserId
        };
    }

    public void Start()
    {
        EnsureStatus(OnboardingStatus.Draft);
        Status = OnboardingStatus.InProgress;
    }

    public void MarkSubscriptionCreated()
    {
        EnsureStatus(OnboardingStatus.InProgress);
        EnsureStep(OnboardingStep.None);
        CurrentStep = OnboardingStep.SubscriptionCreated;
    }

    public void MarkTenantCreated()
    {
        EnsureStatus(OnboardingStatus.InProgress);
        EnsureStep(OnboardingStep.SubscriptionCreated);
        CurrentStep = OnboardingStep.TenantCreated;
    }

    public void MarkOwnerCompanyCreated(Guid companyId)
    {
        if (companyId == Guid.Empty)
            throw new AccountDomainException("OwnerCompanyId cannot be empty.");

        EnsureStatus(OnboardingStatus.InProgress);
        EnsureStep(OnboardingStep.TenantCreated);
        OwnerCompanyId = companyId;
        CurrentStep = OnboardingStep.OwnerCompanyCreated;
    }

    public void MarkAdminProvisioningRequested()
    {
        EnsureStatus(OnboardingStatus.InProgress);
        EnsureStep(OnboardingStep.OwnerCompanyCreated);
        Status = OnboardingStatus.WaitingForExternalProvisioning;
        CurrentStep = OnboardingStep.AdminProvisioningRequested;
    }

    public void MarkAdminUserProvisioned(Guid userId)
    {
        if (userId == Guid.Empty)
            throw new AccountDomainException("AdminUserId cannot be empty.");

        EnsureStatus(OnboardingStatus.WaitingForExternalProvisioning);
        EnsureStep(OnboardingStep.AdminProvisioningRequested);
        AdminUserId = userId;
        Status = OnboardingStatus.InProgress;
        CurrentStep = OnboardingStep.AdminUserProvisioned;
    }

    public void MarkAdminMembershipCreated()
    {
        EnsureStatus(OnboardingStatus.InProgress);
        EnsureStep(OnboardingStep.AdminUserProvisioned);
        CurrentStep = OnboardingStep.AdminMembershipCreated;
    }

    public void MarkActivationRequested()
    {
        EnsureStatus(OnboardingStatus.InProgress);
        EnsureStep(OnboardingStep.AdminMembershipCreated);
        Status = OnboardingStatus.WaitingForActivationDelivery;
        CurrentStep = OnboardingStep.ActivationRequested;
    }

    public void MarkActivationDelivered()
    {
        EnsureStatus(OnboardingStatus.WaitingForActivationDelivery);
        EnsureStep(OnboardingStep.ActivationRequested);
        Status = OnboardingStatus.WaitingForUserActivation;
        CurrentStep = OnboardingStep.ActivationDelivered;
        ActivationDeliveredAtUtc = DateTimeOffset.UtcNow;
    }

    public void MarkUserActivated()
    {
        EnsureStatus(OnboardingStatus.WaitingForUserActivation);
        EnsureStep(OnboardingStep.ActivationDelivered);
        Status = OnboardingStatus.Completed;
        CurrentStep = OnboardingStep.Completed;
        UserActivatedAtUtc = DateTimeOffset.UtcNow;
        CompletedAtUtc = DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// V1 convenience: completes the onboarding from AdminMembershipCreated
    /// when activation steps are not yet implemented.
    /// </summary>
    public void Complete()
    {
        if (OwnerCompanyId is null)
            throw new AccountDomainException("Cannot complete onboarding without an Owner Company.");

        if (AdminUserId is null)
            throw new AccountDomainException("Cannot complete onboarding without an Admin User.");

        Status = OnboardingStatus.Completed;
        CurrentStep = OnboardingStep.Completed;
        CompletedAtUtc = DateTimeOffset.UtcNow;
    }

    public void Fail(string errorCode, string errorMessage)
    {
        if (Status == OnboardingStatus.Completed)
            throw new AccountDomainException("Cannot fail a completed onboarding process.");

        FailedStep = CurrentStep;
        Status = OnboardingStatus.Failed;
        ErrorCode = errorCode;
        ErrorMessage = errorMessage;
        FailedAtUtc = DateTimeOffset.UtcNow;
    }

    public void Retry()
    {
        if (Status != OnboardingStatus.Failed)
            throw new AccountDomainException("Only a failed onboarding process can be retried.");

        Status = OnboardingStatus.InProgress;
        ErrorCode = null;
        ErrorMessage = null;
        FailedAtUtc = null;
        RetryCount++;
        LastRetryAtUtc = DateTimeOffset.UtcNow;
    }

    private void EnsureStatus(OnboardingStatus expected)
    {
        if (Status != expected)
            throw new AccountDomainException(
                $"Invalid status transition. Expected '{expected}' but current status is '{Status}'.");
    }

    private void EnsureStep(OnboardingStep expected)
    {
        if (CurrentStep != expected)
            throw new AccountDomainException(
                $"Invalid step transition. Expected '{expected}' but current step is '{CurrentStep}'.");
    }
}
