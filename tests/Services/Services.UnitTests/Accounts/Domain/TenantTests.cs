using Miqa.Services.Accounts.Domain.Events;
using Miqa.Services.Accounts.Domain.Exceptions;
using Miqa.Services.Accounts.Domain.Models.TenantAggregate;

namespace Services.UnitTests.Accounts.Domain;

public class TenantTests
{
    [Fact]
    public void Create_WithValidData_CreatesTenantWithPendingStatus()
    {
        var internalUserId = Guid.NewGuid();

        var tenant = Tenant.Create("Acme Tenant", internalUserId);

        tenant.Name.Should().Be("Acme Tenant");
        tenant.Status.Should().Be(TenantStatus.Pending);
        tenant.OwnerCompanyId.Should().BeNull();
        tenant.CreatedByInternalUserId.Should().Be(internalUserId);
    }

    [Fact]
    public void Create_WithLeadingTrailingSpaces_TrimsName()
    {
        var tenant = Tenant.Create("  Acme Tenant  ", Guid.NewGuid());

        tenant.Name.Should().Be("Acme Tenant");
    }

    [Fact]
    public void Create_RaisesTenantCreatedDomainEvent()
    {
        var tenant = Tenant.Create("Acme Tenant", Guid.NewGuid());

        tenant.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<TenantCreatedDomainEvent>()
            .Which.TenantId.Should().Be(tenant.Id);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithEmptyName_ThrowsAccountDomainException(string name)
    {
        var act = () => Tenant.Create(name, Guid.NewGuid());

        act.Should().Throw<AccountDomainException>();
    }

    [Fact]
    public void Create_WithEmptyCreatedByInternalUserId_ThrowsAccountDomainException()
    {
        var act = () => Tenant.Create("Acme", Guid.Empty);

        act.Should().Throw<AccountDomainException>();
    }

    [Fact]
    public void SetOwnerCompany_WithValidId_SetsOwnerCompanyId()
    {
        var tenant = Tenant.Create("Acme", Guid.NewGuid());
        var companyId = Guid.NewGuid();

        tenant.SetOwnerCompany(companyId);

        tenant.OwnerCompanyId.Should().Be(companyId);
    }

    [Fact]
    public void SetOwnerCompany_WithEmptyId_ThrowsAccountDomainException()
    {
        var tenant = Tenant.Create("Acme", Guid.NewGuid());

        var act = () => tenant.SetOwnerCompany(Guid.Empty);

        act.Should().Throw<AccountDomainException>();
    }

    [Fact]
    public void Activate_WhenOwnerCompanySet_SetsStatusToActiveAndRecordsTimestamp()
    {
        var tenant = Tenant.Create("Acme", Guid.NewGuid());
        tenant.SetOwnerCompany(Guid.NewGuid());

        tenant.Activate();

        tenant.Status.Should().Be(TenantStatus.Active);
        tenant.ActivatedAtUtc.Should().NotBeNull();
    }

    [Fact]
    public void Activate_WhenOwnerCompanyNotSet_ThrowsAccountDomainException()
    {
        var tenant = Tenant.Create("Acme", Guid.NewGuid());

        var act = () => tenant.Activate();

        act.Should().Throw<AccountDomainException>();
    }

    [Fact]
    public void Suspend_WhenActive_SetsStatusToSuspended()
    {
        var tenant = Tenant.Create("Acme", Guid.NewGuid());
        tenant.SetOwnerCompany(Guid.NewGuid());
        tenant.Activate();

        tenant.Suspend();

        tenant.Status.Should().Be(TenantStatus.Suspended);
    }

    [Fact]
    public void Suspend_WhenNotActive_ThrowsAccountDomainException()
    {
        var tenant = Tenant.Create("Acme", Guid.NewGuid());

        var act = () => tenant.Suspend();

        act.Should().Throw<AccountDomainException>();
    }

    [Fact]
    public void Close_SetsStatusToClosedAndTimestamp()
    {
        var tenant = Tenant.Create("Acme", Guid.NewGuid());
        tenant.SetOwnerCompany(Guid.NewGuid());
        tenant.Activate();

        tenant.Close();

        tenant.Status.Should().Be(TenantStatus.Closed);
        tenant.ClosedAtUtc.Should().NotBeNull();
    }

    [Fact]
    public void Close_WhenAlreadyClosed_ThrowsAccountDomainException()
    {
        var tenant = Tenant.Create("Acme", Guid.NewGuid());
        tenant.SetOwnerCompany(Guid.NewGuid());
        tenant.Activate();
        tenant.Close();

        var act = () => tenant.Close();

        act.Should().Throw<AccountDomainException>();
    }
}
