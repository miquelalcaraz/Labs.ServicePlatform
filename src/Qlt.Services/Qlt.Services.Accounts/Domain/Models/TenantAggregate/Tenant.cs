using Qlt.Domain.SeedWork;
using Qlt.Services.Accounts.Domain.Events;
using Qlt.Services.Accounts.Domain.Exceptions;

namespace Qlt.Services.Accounts.Domain.Models.TenantAggregate;

public class Tenant : Entity, IAggregateRoot
{
    public string Name { get; private set; } = null!;

    public TenantStatus Status { get; private set; }

    public Guid? OwnerCompanyId { get; private set; }

    public Guid CreatedByInternalUserId { get; private set; }

    public DateTimeOffset? ActivatedAtUtc { get; private set; }

    public DateTimeOffset? ClosedAtUtc { get; private set; }

    protected Tenant() { }

    public static Tenant Create(string name, Guid createdByInternalUserId)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new AccountDomainException("Tenant name cannot be empty.");

        if (createdByInternalUserId == Guid.Empty)
            throw new AccountDomainException("CreatedByInternalUserId is required.");

        var tenant = new Tenant
        {
            Name = name.Trim(),
            Status = TenantStatus.Pending,
            CreatedByInternalUserId = createdByInternalUserId
        };

        tenant.AddDomainEvent(new TenantCreatedDomainEvent(tenant.Id, tenant.Name));
        return tenant;
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
            throw new AccountDomainException("Cannot activate a tenant without an Owner Company.");

        Status = TenantStatus.Active;
        ActivatedAtUtc = DateTimeOffset.UtcNow;
    }

    public void Suspend()
    {
        if (Status != TenantStatus.Active)
            throw new AccountDomainException("Only active tenants can be suspended.");

        Status = TenantStatus.Suspended;
    }

    public void Close()
    {
        if (Status == TenantStatus.Closed)
            throw new AccountDomainException("Tenant is already closed.");

        Status = TenantStatus.Closed;
        ClosedAtUtc = DateTimeOffset.UtcNow;
    }
}
