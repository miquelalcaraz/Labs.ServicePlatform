using Microsoft.Extensions.Logging;

using Qlt.Infrastructure.Identity.IntegrationEvents.EventHandling;
using Qlt.Infrastructure.Identity.Models;
using Qlt.Infrastructure.Identity.Repositories;
using Qlt.Services.Accounts.Contracts.IntegrationEvents;

namespace Services.UnitTests.Identity.IntegrationEvents;

public class TenantCreatedIntegrationEventHandlerTests : IdentityTestBase
{
    [Fact]
    public async Task Handle_WhenTenantDoesNotExistLocally_CreatesTenant()
    {
        var (tenantRepo, _, _) = CreateIdentityMocks();
        tenantRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync((Tenant?)null);
        tenantRepo.Setup(r => r.CreateAsync(It.IsAny<Tenant>())).ReturnsAsync<Tenant, ITenantRepository, Tenant>(t => t);

        var handler = CreateHandler(tenantRepo);
        var @event = CreateValidTenantCreatedEvent();

        await handler.Handle(@event);

        tenantRepo.Verify(r => r.CreateAsync(It.Is<Tenant>(t =>
            t.Id == @event.TenantId &&
            t.Name == @event.Name)),
            Times.Once);
    }

    [Fact]
    public async Task Handle_WhenTenantAlreadyExistsLocally_SkipsCreation()
    {
        var existingTenant = Tenant.Create(
            id: TenantId,
            companyId: CompanyId,
            name: "Acme Tenant");

        var (tenantRepo, _, _) = CreateIdentityMocks();
        tenantRepo.Setup(r => r.GetByIdAsync(TenantId)).ReturnsAsync(existingTenant);

        var handler = CreateHandler(tenantRepo);
        var @event = CreateValidTenantCreatedEvent(tenantId: TenantId);

        await handler.Handle(@event);

        tenantRepo.Verify(r => r.CreateAsync(It.IsAny<Tenant>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenOwnerCompanyIdIsNull_CreatesTenantWithEmptyCompanyId()
    {
        var (tenantRepo, _, _) = CreateIdentityMocks();
        tenantRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync((Tenant?)null);
        tenantRepo.Setup(r => r.CreateAsync(It.IsAny<Tenant>())).ReturnsAsync<Tenant, ITenantRepository, Tenant>(t => t);

        var handler = CreateHandler(tenantRepo);
        var @event = new TenantCreatedIntegrationEvent(
            TenantId: Guid.NewGuid(),
            Name: "Acme Tenant",
            OwnerCompanyId: null,
            CreatedByInternalUserId: Guid.NewGuid());

        await handler.Handle(@event);

        tenantRepo.Verify(r => r.CreateAsync(It.Is<Tenant>(t => t.CompanyId == Guid.Empty)), Times.Once);
    }

    private static TenantCreatedIntegrationEventHandler CreateHandler(Mock<ITenantRepository> tenantRepo) =>
        new(
            new Mock<ILogger<TenantCreatedIntegrationEventHandler>>().Object,
            tenantRepo.Object);
}
