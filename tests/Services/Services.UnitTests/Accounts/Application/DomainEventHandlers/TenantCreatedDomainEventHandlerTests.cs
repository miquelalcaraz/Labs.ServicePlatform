using Microsoft.Extensions.Logging;

using Qlt.Platform.Messaging.Services;
using Qlt.Services.Accounts.Application.DomainEventHandlers;
using Qlt.Services.Accounts.Contracts.IntegrationEvents;
using Qlt.Services.Accounts.Domain.Events;
using Qlt.Services.Accounts.Domain.Models.TenantAggregate;
using Qlt.Services.Accounts.Domain.Repositories;

namespace Services.UnitTests.Accounts.Application.DomainEventHandlers;

public class TenantCreatedDomainEventHandlerTests
{
    [Fact]
    public async Task Handle_WhenTenantExists_PublishesTenantCreatedIntegrationEvent()
    {
        var tenant = Tenant.Create("Acme Tenant", Guid.NewGuid());
        tenant.SetOwnerCompany(Guid.NewGuid());

        var tenantRepo = new Mock<ITenantRepository>();
        tenantRepo.Setup(r => r.GetAsync(tenant.Id)).ReturnsAsync(tenant);

        var outbox = new Mock<IEventBusOutboxService>();
        var handler = CreateHandler(tenantRepo, outbox);

        await handler.Handle(new TenantCreatedDomainEvent(tenant.Id, tenant.Name), CancellationToken.None);

        outbox.Verify(o => o.AddAndSaveEventAsync(
            It.Is<TenantCreatedIntegrationEvent>(e =>
                e.TenantId == tenant.Id &&
                e.Name == tenant.Name &&
                e.OwnerCompanyId == tenant.OwnerCompanyId &&
                e.CreatedByInternalUserId == tenant.CreatedByInternalUserId)),
            Times.Once);
    }

    [Fact]
    public async Task Handle_WhenTenantNotFound_DoesNotPublishEvent()
    {
        var tenantRepo = new Mock<ITenantRepository>();
        tenantRepo.Setup(r => r.GetAsync(It.IsAny<Guid>())).ReturnsAsync((Tenant?)null);

        var outbox = new Mock<IEventBusOutboxService>();
        var handler = CreateHandler(tenantRepo, outbox);

        await handler.Handle(new TenantCreatedDomainEvent(Guid.NewGuid(), "Ghost Tenant"), CancellationToken.None);

        outbox.Verify(o => o.AddAndSaveEventAsync(It.IsAny<TenantCreatedIntegrationEvent>()), Times.Never);
    }

    private static TenantCreatedDomainEventHandler CreateHandler(
        Mock<ITenantRepository> tenantRepo,
        Mock<IEventBusOutboxService> outbox) =>
        new(
            new Mock<ILogger<TenantCreatedDomainEventHandler>>().Object,
            tenantRepo.Object,
            outbox.Object);
}
