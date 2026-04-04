using Qlt.Domain.SeedWork;
using Qlt.Services.Accounts.Domain.Events;
using Qlt.Services.Accounts.Domain.Exceptions;

namespace Qlt.Services.Accounts.Domain.Models.SubscriptionAggregate;

public class Subscription : Entity, IAggregateRoot
{
    public Guid TenantId { get; private set; }

    public Guid? OwnerCompanyId { get; private set; }

    public string PlanCode { get; private set; } = null!;

    public SubscriptionStatus Status { get; private set; }

    public DateTimeOffset? StartDateUtc { get; private set; }

    public DateTimeOffset? EndDateUtc { get; private set; }

    public Guid CreatedByInternalUserId { get; private set; }

    public DateTimeOffset? ActivatedAtUtc { get; private set; }

    public DateTimeOffset? SuspendedAtUtc { get; private set; }

    public DateTimeOffset? CancelledAtUtc { get; private set; }

    protected Subscription() { }

    public static Subscription Create(Guid tenantId, string planCode, Guid createdByInternalUserId)
    {
        if (tenantId == Guid.Empty)
            throw new AccountDomainException("TenantId is required.");

        if (string.IsNullOrWhiteSpace(planCode))
            throw new AccountDomainException("PlanCode cannot be empty.");

        if (createdByInternalUserId == Guid.Empty)
            throw new AccountDomainException("CreatedByInternalUserId is required.");

        var subscription = new Subscription
        {
            TenantId = tenantId,
            PlanCode = planCode.Trim(),
            Status = SubscriptionStatus.PendingInitialization,
            CreatedByInternalUserId = createdByInternalUserId
        };

        subscription.AddDomainEvent(
            new SubscriptionCreatedDomainEvent(subscription.Id, tenantId, planCode));
        return subscription;
    }

    public void SetOwnerCompany(Guid companyId)
    {
        if (companyId == Guid.Empty)
            throw new AccountDomainException("OwnerCompanyId cannot be empty.");

        OwnerCompanyId = companyId;
    }

    public void Activate()
    {
        if (OwnerCompanyId is null)
            throw new AccountDomainException("Cannot activate a subscription without an Owner Company.");

        Status = SubscriptionStatus.Active;
        StartDateUtc = DateTimeOffset.UtcNow;
        ActivatedAtUtc = DateTimeOffset.UtcNow;
    }

    public void Suspend()
    {
        if (Status != SubscriptionStatus.Active)
            throw new AccountDomainException("Only active subscriptions can be suspended.");

        Status = SubscriptionStatus.Suspended;
        SuspendedAtUtc = DateTimeOffset.UtcNow;
    }

    public void Cancel()
    {
        if (Status == SubscriptionStatus.Cancelled)
            throw new AccountDomainException("Subscription is already cancelled.");

        Status = SubscriptionStatus.Cancelled;
        CancelledAtUtc = DateTimeOffset.UtcNow;
    }
}
