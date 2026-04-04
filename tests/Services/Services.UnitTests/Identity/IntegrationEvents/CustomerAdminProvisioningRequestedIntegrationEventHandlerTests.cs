using Microsoft.Extensions.Logging;

using Qlt.Infrastructure.Identity.Contracts.IntegrationEvents;
using Qlt.Infrastructure.Identity.IntegrationEvents.EventHandling;
using Qlt.Infrastructure.Identity.Models;
using Qlt.Infrastructure.Identity.Repositories;
using Qlt.Platform.Messaging.Services;

namespace Services.UnitTests.Identity.IntegrationEvents;

public class CustomerAdminProvisioningRequestedIntegrationEventHandlerTests : IdentityTestBase
{
    [Fact]
    public async Task Handle_WhenUserDoesNotExist_CreatesUserAndPublishesEvent()
    {
        var createdUser = ApplicationUser.CreateAsUnConfirmed(
            userName: "admin@acme.com",
            email: "admin@acme.com",
            tenantId: TenantId,
            firstName: "John",
            lastName: "Doe");

        var (tenantRepo, userRepo, outbox) = CreateIdentityMocks();
        tenantRepo.Setup(r => r.GetByIdAsync(TenantId)).ReturnsAsync(
            Tenant.Create(TenantId, CompanyId, "Acme Tenant"));
        userRepo.Setup(r => r.GetByEmailAsync("admin@acme.com")).ReturnsAsync((ApplicationUser?)null);
        userRepo.Setup(r => r.CreateAsync(It.IsAny<ApplicationUser>())).ReturnsAsync(createdUser);

        var handler = CreateHandler(tenantRepo, userRepo, outbox);
        var @event = CreateValidProvisioningEvent(tenantId: TenantId, companyId: CompanyId);

        await handler.Handle(@event);

        userRepo.Verify(r => r.CreateAsync(It.Is<ApplicationUser>(u =>
            u.Email == @event.AdminEmail &&
            u.TenantId == @event.TenantId &&
            u.FirstName == @event.AdminFirstName &&
            u.LastName == @event.AdminLastName)),
            Times.Once);

        outbox.Verify(o => o.AddAndSaveEventAsync(
            It.Is<ExternalUserProvisionedIntegrationEvent>(e =>
                e.TenantId == @event.TenantId &&
                e.CompanyId == @event.CompanyId &&
                e.Email == @event.AdminEmail)),
            Times.Once);
    }

    [Fact]
    public async Task Handle_WhenUserAlreadyExists_SkipsCreationAndPublishesEventWithExistingUser()
    {
        var existingUser = ApplicationUser.CreateAsUnConfirmed(
            userName: "admin@acme.com",
            email: "admin@acme.com",
            tenantId: TenantId);

        var (tenantRepo, userRepo, outbox) = CreateIdentityMocks();
        tenantRepo.Setup(r => r.GetByIdAsync(TenantId)).ReturnsAsync(
            Tenant.Create(TenantId, CompanyId, "Acme Tenant"));
        userRepo.Setup(r => r.GetByEmailAsync("admin@acme.com")).ReturnsAsync(existingUser);

        var handler = CreateHandler(tenantRepo, userRepo, outbox);
        var @event = CreateValidProvisioningEvent(tenantId: TenantId, companyId: CompanyId);

        await handler.Handle(@event);

        userRepo.Verify(r => r.CreateAsync(It.IsAny<ApplicationUser>()), Times.Never);
        outbox.Verify(o => o.AddAndSaveEventAsync(It.IsAny<ExternalUserProvisionedIntegrationEvent>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenTenantDoesNotExistLocally_CreatesTenantBeforeProvisioningUser()
    {
        var createdUser = ApplicationUser.CreateAsUnConfirmed(
            userName: "admin@acme.com",
            email: "admin@acme.com",
            tenantId: TenantId);

        var (tenantRepo, userRepo, outbox) = CreateIdentityMocks();
        tenantRepo.Setup(r => r.GetByIdAsync(TenantId)).ReturnsAsync((Tenant?)null);
        tenantRepo.Setup(r => r.CreateAsync(It.IsAny<Tenant>()))
            .ReturnsAsync<Tenant, ITenantRepository, Tenant>(t => t);
        userRepo.Setup(r => r.GetByEmailAsync(It.IsAny<string>())).ReturnsAsync((ApplicationUser?)null);
        userRepo.Setup(r => r.CreateAsync(It.IsAny<ApplicationUser>())).ReturnsAsync(createdUser);

        var handler = CreateHandler(tenantRepo, userRepo, outbox);
        var @event = CreateValidProvisioningEvent(tenantId: TenantId, companyId: CompanyId);

        await handler.Handle(@event);

        tenantRepo.Verify(r => r.CreateAsync(It.Is<Tenant>(t =>
            t.Id == @event.TenantId &&
            t.CompanyId == @event.CompanyId &&
            t.Name == @event.TenantName)),
            Times.Once);
    }

    private static CustomerAdminProvisioningRequestedIntegrationEventHandler CreateHandler(
        Mock<ITenantRepository> tenantRepo,
        Mock<IUserRepository> userRepo,
        Mock<IEventBusOutboxService> outbox) =>
        new(
            new Mock<ILogger<CustomerAdminProvisioningRequestedIntegrationEventHandler>>().Object,
            tenantRepo.Object,
            userRepo.Object,
            outbox.Object);
}
