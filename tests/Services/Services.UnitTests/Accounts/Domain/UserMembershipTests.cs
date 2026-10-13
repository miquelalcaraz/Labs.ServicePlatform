using Sirius.Services.Accounts.Domain.Exceptions;
using Sirius.Services.Accounts.Domain.Models.UserMembershipAggregate;

namespace Services.UnitTests.Accounts.Domain;

public class UserMembershipTests
{
    [Fact]
    public void Create_WithValidData_CreatesMembershipWithPendingActivationStatus()
    {
        var userId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var companyId = Guid.NewGuid();

        var membership = UserMembership.Create(
            userId, tenantId, companyId,
            MembershipType.External, MembershipRole.TenantAdmin,
            isPrimary: true);

        membership.UserId.Should().Be(userId);
        membership.TenantId.Should().Be(tenantId);
        membership.CompanyId.Should().Be(companyId);
        membership.MembershipType.Should().Be(MembershipType.External);
        membership.RoleCode.Should().Be(MembershipRole.TenantAdmin);
        membership.Status.Should().Be(MembershipStatus.PendingActivation);
        membership.IsPrimary.Should().BeTrue();
    }

    [Fact]
    public void Create_WithIsPrimaryFalse_SetsIsPrimaryFalse()
    {
        var membership = UserMembership.Create(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            MembershipType.External, MembershipRole.CompanyUser,
            isPrimary: false);

        membership.IsPrimary.Should().BeFalse();
    }

    [Fact]
    public void Create_WithEmptyUserId_ThrowsAccountDomainException()
    {
        var act = () => UserMembership.Create(
            Guid.Empty, Guid.NewGuid(), Guid.NewGuid(),
            MembershipType.External, MembershipRole.TenantAdmin, isPrimary: true);

        act.Should().Throw<AccountDomainException>();
    }

    [Fact]
    public void Create_WithEmptyTenantId_ThrowsAccountDomainException()
    {
        var act = () => UserMembership.Create(
            Guid.NewGuid(), Guid.Empty, Guid.NewGuid(),
            MembershipType.External, MembershipRole.TenantAdmin, isPrimary: true);

        act.Should().Throw<AccountDomainException>();
    }

    [Fact]
    public void Create_WithEmptyCompanyId_ThrowsAccountDomainException()
    {
        var act = () => UserMembership.Create(
            Guid.NewGuid(), Guid.NewGuid(), Guid.Empty,
            MembershipType.External, MembershipRole.TenantAdmin, isPrimary: true);

        act.Should().Throw<AccountDomainException>();
    }

    [Fact]
    public void Activate_UpdatesStatusToActive()
    {
        var membership = UserMembership.Create(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            MembershipType.External, MembershipRole.TenantAdmin, isPrimary: true);

        membership.Activate();

        membership.Status.Should().Be(MembershipStatus.Active);
    }

    [Fact]
    public void Activate_SetsActivatedAtUtcTimestamp()
    {
        var membership = UserMembership.Create(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            MembershipType.External, MembershipRole.TenantAdmin, isPrimary: true);
        var before = DateTimeOffset.UtcNow;

        membership.Activate();

        membership.ActivatedAtUtc.Should().NotBeNull();
        membership.ActivatedAtUtc.Should().BeOnOrAfter(before).And.BeOnOrBefore(DateTimeOffset.UtcNow);
    }

    [Fact]
    public void CreateExternalAdminMembership_CreatesExternalTenantAdminWithPrimaryFlag()
    {
        var userId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var companyId = Guid.NewGuid();

        var membership = UserMembership.CreateExternalAdminMembership(userId, tenantId, companyId);

        membership.UserId.Should().Be(userId);
        membership.TenantId.Should().Be(tenantId);
        membership.CompanyId.Should().Be(companyId);
        membership.MembershipType.Should().Be(MembershipType.External);
        membership.RoleCode.Should().Be(MembershipRole.TenantAdmin);
        membership.IsPrimary.Should().BeTrue();
        membership.Status.Should().Be(MembershipStatus.PendingActivation);
    }

    [Fact]
    public void CreateExternalAdminMembership_WithCreatedByInternalUserId_SetsValue()
    {
        var internalUserId = Guid.NewGuid();

        var membership = UserMembership.CreateExternalAdminMembership(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), internalUserId);

        membership.CreatedByInternalUserId.Should().Be(internalUserId);
    }

    [Fact]
    public void Revoke_SetsStatusToRevokedAndTimestamp()
    {
        var membership = UserMembership.Create(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            MembershipType.External, MembershipRole.TenantAdmin, isPrimary: true);
        membership.Activate();

        membership.Revoke();

        membership.Status.Should().Be(MembershipStatus.Revoked);
        membership.RevokedAtUtc.Should().NotBeNull();
    }

    [Fact]
    public void Revoke_WhenAlreadyRevoked_ThrowsAccountDomainException()
    {
        var membership = UserMembership.Create(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            MembershipType.External, MembershipRole.TenantAdmin, isPrimary: true);
        membership.Activate();
        membership.Revoke();

        var act = () => membership.Revoke();

        act.Should().Throw<AccountDomainException>();
    }

    [Fact]
    public void ChangeRole_UpdatesRoleCode()
    {
        var membership = UserMembership.Create(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            MembershipType.External, MembershipRole.TenantAdmin, isPrimary: true);

        membership.ChangeRole(MembershipRole.CompanyUser);

        membership.RoleCode.Should().Be(MembershipRole.CompanyUser);
    }

    [Fact]
    public void ChangeRole_WhenRevoked_ThrowsAccountDomainException()
    {
        var membership = UserMembership.Create(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            MembershipType.External, MembershipRole.TenantAdmin, isPrimary: true);
        membership.Activate();
        membership.Revoke();

        var act = () => membership.ChangeRole(MembershipRole.CompanyUser);

        act.Should().Throw<AccountDomainException>();
    }
}
