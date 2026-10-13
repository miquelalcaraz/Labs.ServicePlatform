using Sirius.Domain.SeedWork;
using Sirius.Services.Accounts.Domain.Events;
using Sirius.Services.Accounts.Domain.Exceptions;

namespace Sirius.Services.Accounts.Domain.Models.CustomerActivationProcessAggregate;

public class CustomerActivationProcess : Entity, IAggregateRoot
{
    public Guid SubscriptionId { get; private set; }

    public Guid TenantId { get; private set; }

    public Guid OwnerCompanyId { get; private set; }

    public Guid AdminUserId { get; private set; }

    public Guid AdminMembershipId { get; private set; }

    public ActivationStatus Status { get; private set; }

    public ActivationStep CurrentStep { get; private set; }

    public int RetryCount { get; private set; }

    public string? LastErrorCode { get; private set; }

    public string? LastErrorMessage { get; private set; }

    public DateTimeOffset StartedAtUtc { get; private set; }

    public DateTimeOffset? CompletedAtUtc { get; private set; }

    public DateTimeOffset? ActivationRequestedAtUtc { get; private set; }

    public DateTimeOffset? ActivationDeliveredAtUtc { get; private set; }

    public DateTimeOffset? UserActivatedAtUtc { get; private set; }

    public Guid? CorrelationId { get; private set; }

    public string? CompanyLegalName { get; private set; }

    public string? SubscriptionPlanCode { get; private set; }

    public string? AdminEmail { get; private set; }

    public DateTimeOffset? LastRetryAtUtc { get; private set; }

    protected CustomerActivationProcess() { }

    public static CustomerActivationProcess Create(
        Guid subscriptionId,
        Guid tenantId,
        Guid ownerCompanyId,
        Guid adminUserId,
        Guid adminMembershipId,
        Guid? correlationId = null,
        string? companyLegalName = null,
        string? subscriptionPlanCode = null,
        string? adminEmail = null)
    {
        if (subscriptionId == Guid.Empty)
            throw new AccountDomainException("SubscriptionId is required.");

        if (tenantId == Guid.Empty)
            throw new AccountDomainException("TenantId is required.");

        if (ownerCompanyId == Guid.Empty)
            throw new AccountDomainException("OwnerCompanyId is required.");

        if (adminUserId == Guid.Empty)
            throw new AccountDomainException("AdminUserId is required.");

        if (adminMembershipId == Guid.Empty)
            throw new AccountDomainException("AdminMembershipId is required.");

        var process = new CustomerActivationProcess
        {
            SubscriptionId = subscriptionId,
            TenantId = tenantId,
            OwnerCompanyId = ownerCompanyId,
            AdminUserId = adminUserId,
            AdminMembershipId = adminMembershipId,
            Status = ActivationStatus.Draft,
            CurrentStep = ActivationStep.None,
            StartedAtUtc = DateTimeOffset.UtcNow,
            CorrelationId = correlationId,
            CompanyLegalName = companyLegalName,
            SubscriptionPlanCode = subscriptionPlanCode,
            AdminEmail = adminEmail
        };

        process.AddDomainEvent(new CustomerActivationProcessStartedDomainEvent(process.Id));

        return process;
    }

    public void Start()
    {
        EnsureStatus(ActivationStatus.Draft);
        Status = ActivationStatus.InProgress;
    }

    public void RequestActivation()
    {
        EnsureStatus(ActivationStatus.InProgress);
        EnsureStep(ActivationStep.None);

        CurrentStep = ActivationStep.ActivationRequested;
        Status = ActivationStatus.WaitingForNotificationDelivery;
        ActivationRequestedAtUtc = DateTimeOffset.UtcNow;

        AddDomainEvent(new ActivationEmailRequestedDomainEvent(
            Id,
            AdminUserId,
            TenantId));
    }

    public void MarkActivationDelivered()
    {
        EnsureStatus(ActivationStatus.WaitingForNotificationDelivery);
        EnsureStep(ActivationStep.ActivationRequested);

        CurrentStep = ActivationStep.ActivationDelivered;
        Status = ActivationStatus.WaitingForUserActivation;
        ActivationDeliveredAtUtc = DateTimeOffset.UtcNow;
    }

    public void MarkUserActivated(Guid userId)
    {
        EnsureStatus(ActivationStatus.WaitingForUserActivation);
        EnsureStep(ActivationStep.ActivationDelivered);

        if (userId != AdminUserId)
            throw new AccountDomainException("The activated user does not match the expected admin user.");

        CurrentStep = ActivationStep.UserActivated;
        Status = ActivationStatus.InProgress;
        UserActivatedAtUtc = DateTimeOffset.UtcNow;

        AddDomainEvent(new CustomerAdminActivatedDomainEvent(Id, userId));
    }

    public void MarkCustomerReady()
    {
        EnsureStatus(ActivationStatus.InProgress);
        EnsureStep(ActivationStep.UserActivated);

        CurrentStep = ActivationStep.CustomerReady;
    }

    public void Complete()
    {
        EnsureStatus(ActivationStatus.InProgress);
        EnsureStep(ActivationStep.CustomerReady);

        Status = ActivationStatus.Completed;
        CurrentStep = ActivationStep.Completed;
        CompletedAtUtc = DateTimeOffset.UtcNow;

        AddDomainEvent(new CustomerGoLiveDomainEvent(
            SubscriptionId,
            TenantId,
            AdminUserId));
    }

    public void Fail(string errorCode, string errorMessage)
    {
        if (Status == ActivationStatus.Completed)
            throw new AccountDomainException("Cannot fail a completed activation process.");

        Status = ActivationStatus.Failed;
        LastErrorCode = errorCode;
        LastErrorMessage = errorMessage;
    }

    public void Retry()
    {
        EnsureStatus(ActivationStatus.Failed);

        RetryCount++;
        LastRetryAtUtc = DateTimeOffset.UtcNow;
        LastErrorCode = null;
        LastErrorMessage = null;
        Status = ActivationStatus.InProgress;
    }

    public void RestartActivationDelivery()
    {
        EnsureStatus(ActivationStatus.InProgress);
        EnsureStep(ActivationStep.ActivationRequested);

        Status = ActivationStatus.WaitingForNotificationDelivery;
    }

    private void EnsureStatus(ActivationStatus expected)
    {
        if (Status != expected)
            throw new AccountDomainException(
                $"Invalid status transition. Expected '{expected}' but current status is '{Status}'.");
    }

    private void EnsureStep(ActivationStep expected)
    {
        if (CurrentStep != expected)
            throw new AccountDomainException(
                $"Invalid step transition. Expected '{expected}' but current step is '{CurrentStep}'.");
    }
}
