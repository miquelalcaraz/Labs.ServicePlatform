using Miqa.Domain.SeedWork;
using Miqa.Services.Accounts.Domain.Events;
using Miqa.Services.Accounts.Domain.Exceptions;

namespace Miqa.Services.Accounts.Domain.Models.OnboardingAggregate;

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

    public string? CompanyLegalName { get; private set; }

    public string? SubscriptionPlanCode { get; private set; }

    public string? AdminEmail { get; private set; }

    public string? AdminFirstName { get; private set; }

    public string? AdminLastName { get; private set; }

    public string? TenantName { get; private set; }

    public Guid? AdminMembershipId { get; private set; }

    protected OnboardingProcess() { }

    public static OnboardingProcess Create(
        Guid subscriptionId,
        Guid tenantId,
        Guid initiatedByInternalUserId,
        string? companyLegalName = null,
        string? subscriptionPlanCode = null,
        string? adminEmail = null,
        string? adminFirstName = null,
        string? adminLastName = null,
        string? tenantName = null,
        Guid? correlationId = null)
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
            InitiatedByInternalUserId = initiatedByInternalUserId,
            CompanyLegalName = companyLegalName,
            SubscriptionPlanCode = subscriptionPlanCode,
            AdminEmail = adminEmail,
            AdminFirstName = adminFirstName,
            AdminLastName = adminLastName,
            TenantName = tenantName,
            CorrelationId = correlationId
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
        AddDomainEvent(new AdminProvisioningRequestedDomainEvent(Id));
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

    public void MarkAdminMembershipCreated(Guid? membershipId = null)
    {
        EnsureStatus(OnboardingStatus.InProgress);
        EnsureStep(OnboardingStep.AdminUserProvisioned);
        AdminMembershipId = membershipId;
        CurrentStep = OnboardingStep.AdminMembershipCreated;
    }

    /// <summary>
    /// Completes the onboarding process after admin membership has been established.
    /// </summary>
    public void Complete()
    {
        EnsureStatus(OnboardingStatus.InProgress);
        EnsureStep(OnboardingStep.AdminMembershipCreated);
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

    /// <summary>
    /// Restores the waiting state for external provisioning after a retry,
    /// allowing <see cref="MarkAdminUserProvisioned"/> to be called when Identity responds.
    /// </summary>
    public void RestartAdminProvisioning()
    {
        EnsureStatus(OnboardingStatus.InProgress);
        EnsureStep(OnboardingStep.AdminProvisioningRequested);
        Status = OnboardingStatus.WaitingForExternalProvisioning;
        AddDomainEvent(new AdminProvisioningRequestedDomainEvent(Id));
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
