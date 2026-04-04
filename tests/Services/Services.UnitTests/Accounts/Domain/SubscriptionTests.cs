using Qlt.Services.Accounts.Domain.Events;
using Qlt.Services.Accounts.Domain.Exceptions;
using Qlt.Services.Accounts.Domain.Models.SubscriptionAggregate;

namespace Services.UnitTests.Accounts.Domain;

public class SubscriptionTests
{
    [Fact]
    public void Create_WithValidData_CreatesSubscriptionWithPendingStatus()
    {
        var tenantId = Guid.NewGuid();
        var internalUserId = Guid.NewGuid();

        var subscription = Subscription.Create(tenantId, "STANDARD", internalUserId);

        subscription.TenantId.Should().Be(tenantId);
        subscription.PlanCode.Should().Be("STANDARD");
        subscription.Status.Should().Be(SubscriptionStatus.PendingInitialization);
        subscription.OwnerCompanyId.Should().BeNull();
        subscription.CreatedByInternalUserId.Should().Be(internalUserId);
    }

    [Fact]
    public void Create_WithLeadingTrailingSpaces_TrimsPlanCode()
    {
        var subscription = Subscription.Create(Guid.NewGuid(), "  STANDARD  ", Guid.NewGuid());

        subscription.PlanCode.Should().Be("STANDARD");
    }

    [Fact]
    public void Create_RaisesSubscriptionCreatedDomainEvent()
    {
        var tenantId = Guid.NewGuid();

        var subscription = Subscription.Create(tenantId, "STANDARD", Guid.NewGuid());

        subscription.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<SubscriptionCreatedDomainEvent>()
            .Which.SubscriptionId.Should().Be(subscription.Id);
    }

    [Fact]
    public void Create_WithEmptyTenantId_ThrowsAccountDomainException()
    {
        var act = () => Subscription.Create(Guid.Empty, "STANDARD", Guid.NewGuid());

        act.Should().Throw<AccountDomainException>();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithEmptyPlanCode_ThrowsAccountDomainException(string planCode)
    {
        var act = () => Subscription.Create(Guid.NewGuid(), planCode, Guid.NewGuid());

        act.Should().Throw<AccountDomainException>();
    }

    [Fact]
    public void Create_WithEmptyCreatedByInternalUserId_ThrowsAccountDomainException()
    {
        var act = () => Subscription.Create(Guid.NewGuid(), "STANDARD", Guid.Empty);

        act.Should().Throw<AccountDomainException>();
    }

    [Fact]
    public void SetOwnerCompany_WithValidId_SetsOwnerCompanyId()
    {
        var subscription = Subscription.Create(Guid.NewGuid(), "STANDARD", Guid.NewGuid());
        var companyId = Guid.NewGuid();

        subscription.SetOwnerCompany(companyId);

        subscription.OwnerCompanyId.Should().Be(companyId);
    }

    [Fact]
    public void SetOwnerCompany_WithEmptyId_ThrowsAccountDomainException()
    {
        var subscription = Subscription.Create(Guid.NewGuid(), "STANDARD", Guid.NewGuid());

        var act = () => subscription.SetOwnerCompany(Guid.Empty);

        act.Should().Throw<AccountDomainException>();
    }

    [Fact]
    public void Activate_WhenOwnerCompanySet_SetsStatusToActiveAndRecordsStartDate()
    {
        var subscription = Subscription.Create(Guid.NewGuid(), "STANDARD", Guid.NewGuid());
        subscription.SetOwnerCompany(Guid.NewGuid());

        subscription.Activate();

        subscription.Status.Should().Be(SubscriptionStatus.Active);
        subscription.StartDateUtc.Should().NotBeNull();
    }

    [Fact]
    public void Activate_WhenOwnerCompanyNotSet_ThrowsAccountDomainException()
    {
        var subscription = Subscription.Create(Guid.NewGuid(), "STANDARD", Guid.NewGuid());

        var act = () => subscription.Activate();

        act.Should().Throw<AccountDomainException>();
    }

    [Fact]
    public void Activate_SetsActivatedAtUtcTimestamp()
    {
        var subscription = Subscription.Create(Guid.NewGuid(), "STANDARD", Guid.NewGuid());
        subscription.SetOwnerCompany(Guid.NewGuid());
        var before = DateTimeOffset.UtcNow;

        subscription.Activate();

        subscription.ActivatedAtUtc.Should().NotBeNull();
        subscription.ActivatedAtUtc.Should().BeOnOrAfter(before).And.BeOnOrBefore(DateTimeOffset.UtcNow);
    }

    [Fact]
    public void Suspend_WhenActive_SetsStatusToSuspendedAndTimestamp()
    {
        var subscription = Subscription.Create(Guid.NewGuid(), "STANDARD", Guid.NewGuid());
        subscription.SetOwnerCompany(Guid.NewGuid());
        subscription.Activate();

        subscription.Suspend();

        subscription.Status.Should().Be(SubscriptionStatus.Suspended);
        subscription.SuspendedAtUtc.Should().NotBeNull();
    }

    [Fact]
    public void Suspend_WhenNotActive_ThrowsAccountDomainException()
    {
        var subscription = Subscription.Create(Guid.NewGuid(), "STANDARD", Guid.NewGuid());

        var act = () => subscription.Suspend();

        act.Should().Throw<AccountDomainException>();
    }

    [Fact]
    public void Cancel_SetsStatusToCancelledAndTimestamp()
    {
        var subscription = Subscription.Create(Guid.NewGuid(), "STANDARD", Guid.NewGuid());
        subscription.SetOwnerCompany(Guid.NewGuid());
        subscription.Activate();

        subscription.Cancel();

        subscription.Status.Should().Be(SubscriptionStatus.Cancelled);
        subscription.CancelledAtUtc.Should().NotBeNull();
    }

    [Fact]
    public void Cancel_WhenAlreadyCancelled_ThrowsAccountDomainException()
    {
        var subscription = Subscription.Create(Guid.NewGuid(), "STANDARD", Guid.NewGuid());
        subscription.SetOwnerCompany(Guid.NewGuid());
        subscription.Activate();
        subscription.Cancel();

        var act = () => subscription.Cancel();

        act.Should().Throw<AccountDomainException>();
    }
}
