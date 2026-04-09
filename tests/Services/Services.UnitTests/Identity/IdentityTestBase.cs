using Microsoft.Extensions.Logging;

using Qlt.Infrastructure.Identity.Repositories;
using Qlt.Platform.Messaging.Services;
using Qlt.Services.Accounts.Contracts.IntegrationEvents;

namespace Services.UnitTests.Identity;

public abstract class IdentityTestBase
{
    protected static readonly Guid TenantId = Guid.NewGuid();
    protected static readonly Guid CompanyId = Guid.NewGuid();
    protected static readonly Guid SubscriptionId = Guid.NewGuid();
    protected static readonly Guid OnboardingProcessId = Guid.NewGuid();

    protected static CustomerAdminProvisioningRequestedIntegrationEvent CreateValidProvisioningEvent(
        Guid? tenantId = null,
        Guid? companyId = null) =>
        new(
            SubscriptionId: SubscriptionId,
            TenantId: tenantId ?? TenantId,
            CompanyId: companyId ?? CompanyId,
            OnboardingProcessId: OnboardingProcessId,
            AdminEmail: "admin@acme.com",
            AdminFirstName: "John",
            AdminLastName: "Doe",
            TenantName: "Acme Tenant");

    protected static TenantCreatedIntegrationEvent CreateValidTenantCreatedEvent(
        Guid? tenantId = null,
        Guid? ownerCompanyId = null) =>
        new(
            TenantId: tenantId ?? TenantId,
            Name: "Acme Tenant",
            OwnerCompanyId: ownerCompanyId ?? CompanyId,
            CreatedByInternalUserId: Guid.NewGuid());

    protected static (
        Mock<ITenantRepository> TenantRepo,
        Mock<IUserRepository> UserRepo,
        Mock<IEventBusOutboxService> Outbox)
        CreateIdentityMocks()
    {
        var tenantRepo = new Mock<ITenantRepository>();
        var userRepo = new Mock<IUserRepository>();
        var outbox = new Mock<IEventBusOutboxService>();
        return (tenantRepo, userRepo, outbox);
    }

    protected static Mock<ILogger<T>> CreateLogger<T>() => new();
}
