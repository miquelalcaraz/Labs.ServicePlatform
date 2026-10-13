using Microsoft.Extensions.Logging;

using Sirius.Contracts.Accounts.IntegrationEvents;
using Sirius.Platform.Messaging.Services;
using Sirius.Services.Accounts.Application.DomainEventHandlers;
using Sirius.Services.Accounts.Domain.Events;
using Sirius.Services.Accounts.Domain.Models.CustomerActivationProcessAggregate;
using Sirius.Services.Accounts.Domain.Repositories;

namespace Services.UnitTests.Accounts.Application.DomainEventHandlers;

public class CustomerGoLiveDomainEventHandlerTests
{
    [Fact]
    public async Task Handle_WhenProcessExists_PublishesBothIntegrationEvents()
    {
        var process = CustomerActivationProcess.Create(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            companyLegalName: "Acme Corp",
            subscriptionPlanCode: "STANDARD",
            adminEmail: "admin@acme.com");

        var repo = new Mock<ICustomerActivationProcessRepository>();
        repo.Setup(r => r.GetBySubscriptionIdAsync(process.SubscriptionId)).ReturnsAsync(process);

        var outbox = new Mock<IEventBusOutboxService>();
        var handler = CreateHandler(repo, outbox);

        await handler.Handle(
            new CustomerGoLiveDomainEvent(process.SubscriptionId, process.TenantId, process.AdminUserId),
            CancellationToken.None);

        outbox.Verify(o => o.AddAndSaveEventAsync(
            It.Is<CustomerActivationCompletedIntegrationEvent>(e =>
                e.ActivationProcessId == process.Id &&
                e.SubscriptionId == process.SubscriptionId &&
                e.TenantId == process.TenantId &&
                e.OwnerCompanyId == process.OwnerCompanyId &&
                e.AdminUserId == process.AdminUserId &&
                e.AdminMembershipId == process.AdminMembershipId &&
                e.CorrelationId == process.CorrelationId)),
            Times.Once);

        outbox.Verify(o => o.AddAndSaveEventAsync(
            It.Is<CustomerGoLiveIntegrationEvent>(e =>
                e.ActivationProcessId == process.Id &&
                e.SubscriptionId == process.SubscriptionId &&
                e.TenantId == process.TenantId &&
                e.CorrelationId == process.CorrelationId)),
            Times.Once);
    }

    [Fact]
    public async Task Handle_WhenProcessNotFound_DoesNotPublishEvents()
    {
        var repo = new Mock<ICustomerActivationProcessRepository>();
        repo.Setup(r => r.GetBySubscriptionIdAsync(It.IsAny<Guid>())).ReturnsAsync((CustomerActivationProcess?)null);

        var outbox = new Mock<IEventBusOutboxService>();
        var handler = CreateHandler(repo, outbox);

        await handler.Handle(
            new CustomerGoLiveDomainEvent(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()),
            CancellationToken.None);

        outbox.Verify(o => o.AddAndSaveEventAsync(
            It.IsAny<CustomerActivationCompletedIntegrationEvent>()), Times.Never);
        outbox.Verify(o => o.AddAndSaveEventAsync(
            It.IsAny<CustomerGoLiveIntegrationEvent>()), Times.Never);
    }

    private static CustomerGoLiveDomainEventHandler CreateHandler(
        Mock<ICustomerActivationProcessRepository> repo,
        Mock<IEventBusOutboxService> outbox) =>
        new(
            repo.Object,
            outbox.Object,
            new Mock<ILogger<CustomerGoLiveDomainEventHandler>>().Object);
}
