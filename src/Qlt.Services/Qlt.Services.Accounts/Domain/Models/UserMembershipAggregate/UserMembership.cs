using Qlt.Domain.SeedWork;
using Qlt.Services.Accounts.Domain.Exceptions;

namespace Qlt.Services.Accounts.Domain.Models.UserMembershipAggregate;

public class UserMembership : Entity, IAggregateRoot
{
    public Guid UserId { get; private set; }

    public Guid TenantId { get; private set; }

    public Guid CompanyId { get; private set; }

    public MembershipType MembershipType { get; private set; }

    public MembershipRole RoleCode { get; private set; }

    public MembershipStatus Status { get; private set; }

    public bool IsPrimary { get; private set; }

    public Guid CreatedByInternalUserId { get; private set; }

    public DateTimeOffset? ActivatedAtUtc { get; private set; }

    public DateTimeOffset? RevokedAtUtc { get; private set; }

    protected UserMembership() { }

    public static UserMembership CreateExternalAdminMembership(
        Guid userId,
        Guid tenantId,
        Guid companyId,
        Guid createdByInternalUserId = default)
    {
        return Create(userId, tenantId, companyId, MembershipType.External,
            MembershipRole.TenantAdmin, isPrimary: true, createdByInternalUserId);
    }

    public static UserMembership Create(
        Guid userId,
        Guid tenantId,
        Guid companyId,
        MembershipType membershipType,
        MembershipRole roleCode,
        bool isPrimary,
        Guid createdByInternalUserId = default)
    {
        if (userId == Guid.Empty)
            throw new AccountDomainException("UserId is required.");

        if (tenantId == Guid.Empty)
            throw new AccountDomainException("TenantId is required.");

        if (companyId == Guid.Empty)
            throw new AccountDomainException("CompanyId is required.");

        return new UserMembership
        {
            UserId = userId,
            TenantId = tenantId,
            CompanyId = companyId,
            MembershipType = membershipType,
            RoleCode = roleCode,
            Status = MembershipStatus.PendingActivation,
            IsPrimary = isPrimary,
            CreatedByInternalUserId = createdByInternalUserId
        };
    }

    public void Activate()
    {
        Status = MembershipStatus.Active;
        ActivatedAtUtc = DateTimeOffset.UtcNow;
    }

    public void Revoke()
    {
        if (Status == MembershipStatus.Revoked)
            throw new AccountDomainException("Membership is already revoked.");

        Status = MembershipStatus.Revoked;
        RevokedAtUtc = DateTimeOffset.UtcNow;
    }

    public void ChangeRole(MembershipRole newRole)
    {
        if (Status == MembershipStatus.Revoked)
            throw new AccountDomainException("Cannot change role of a revoked membership.");

        RoleCode = newRole;
    }
}
