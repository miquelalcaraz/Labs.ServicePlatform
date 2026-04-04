using Microsoft.Extensions.Logging;

using Qlt.Platform.Messaging.Services;
using Qlt.Services.Accounts.Application.DomainEventHandlers;
using Qlt.Services.Accounts.Contracts.IntegrationEvents;
using Qlt.Services.Accounts.Domain.Events;
using Qlt.Services.Accounts.Domain.Models.SubscriptionAggregate;
using Qlt.Services.Accounts.Domain.Repositories;

namespace Services.UnitTests.Accounts.Application.DomainEventHandlers;

public class SubscriptionCreatedDomainEventHandlerTests
{
    [Fact]
    public async Task Handle_WhenSubscriptionExists_PublishesSubscriptionCreatedIntegrationEvent()
    {
        var tenantId = Guid.NewGuid();
        var subscription = Subscription.Create(tenantId, "STANDARD", Guid.NewGuid());
        subscription.SetOwnerCompany(Guid.NewGuid());

        var subscriptionRepo = new Mock<ISubscriptionRepository>();
        subscriptionRepo.Setup(r => r.GetAsync(subscription.Id)).ReturnsAsync(subscription);

        var outbox = new Mock<IEventBusOutboxService>();
        var handler = CreateHandler(subscriptionRepo, outbox);

        await handler.Handle(
            new SubscriptionCreatedDomainEvent(subscription.Id, tenantId, "STANDARD"),
            CancellationToken.None);

        outbox.Verify(o => o.AddAndSaveEventAsync(
            It.Is<SubscriptionCreatedIntegrationEvent>(e =>
                e.SubscriptionId == subscription.Id &&
                e.TenantId == subscription.TenantId &&
                e.PlanCode == subscription.PlanCode &&
                e.CreatedByInternalUserId == subscription.CreatedByInternalUserId)),
            Times.Once);
    }

    [Fact]
    public async Task Handle_WhenSubscriptionNotFound_DoesNotPublishEvent()
    {
        var subscriptionRepo = new Mock<ISubscriptionRepository>();
        subscriptionRepo.Setup(r => r.GetAsync(It.IsAny<Guid>())).ReturnsAsync((Subscription?)null);

        var outbox = new Mock<IEventBusOutboxService>();
        var handler = CreateHandler(subscriptionRepo, outbox);

        await handler.Handle(
            new SubscriptionCreatedDomainEvent(Guid.NewGuid(), Guid.NewGuid(), "STANDARD"),
            CancellationToken.None);

        outbox.Verify(o => o.AddAndSaveEventAsync(It.IsAny<SubscriptionCreatedIntegrationEvent>()), Times.Never);
    }

    private static SubscriptionCreatedDomainEventHandler CreateHandler(
        Mock<ISubscriptionRepository> subscriptionRepo,
        Mock<IEventBusOutboxService> outbox) =>
        new(
            new Mock<ILogger<SubscriptionCreatedDomainEventHandler>>().Object,
            subscriptionRepo.Object,
            outbox.Object);
}
