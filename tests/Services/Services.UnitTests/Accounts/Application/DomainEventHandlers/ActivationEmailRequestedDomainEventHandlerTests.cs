using Microsoft.Extensions.Logging;

using Miqa.Contracts.Accounts.IntegrationEvents;
using Miqa.Platform.Messaging.Services;
using Miqa.Services.Accounts.Application.DomainEventHandlers;
using Miqa.Services.Accounts.Domain.Events;
using Miqa.Services.Accounts.Domain.Models.CustomerActivationProcessAggregate;
using Miqa.Services.Accounts.Domain.Repositories;

namespace Services.UnitTests.Accounts.Application.DomainEventHandlers;

public class ActivationEmailRequestedDomainEventHandlerTests
{
    [Fact]
    public async Task Handle_WhenProcessExists_PublishesActivationEmailRequestedIntegrationEvent()
    {
        var process = CustomerActivationProcess.Create(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            companyLegalName: "Acme Corp",
            subscriptionPlanCode: "STANDARD",
            adminEmail: "admin@acme.com");
        process.Start();
        process.RequestActivation();

        var repo = new Mock<ICustomerActivationProcessRepository>();
        repo.Setup(r => r.GetAsync(process.Id)).ReturnsAsync(process);

        var outbox = new Mock<IEventBusOutboxService>();
        var handler = CreateHandler(repo, outbox);

        await handler.Handle(
            new ActivationEmailRequestedDomainEvent(process.Id, process.AdminUserId, process.TenantId),
            CancellationToken.None);

        outbox.Verify(o => o.AddAndSaveEventAsync(
            It.Is<ActivationEmailRequestedIntegrationEvent>(e =>
                e.ActivationProcessId == process.Id &&
                e.SubscriptionId == process.SubscriptionId &&
                e.TenantId == process.TenantId &&
                e.OwnerCompanyId == process.OwnerCompanyId &&
                e.AdminUserId == process.AdminUserId &&
                e.AdminEmail == "admin@acme.com" &&
                e.CompanyLegalName == "Acme Corp" &&
                e.SubscriptionPlanCode == "STANDARD")),
            Times.Once);
    }

    [Fact]
    public async Task Handle_WhenProcessNotFound_DoesNotPublishEvent()
    {
        var repo = new Mock<ICustomerActivationProcessRepository>();
        repo.Setup(r => r.GetAsync(It.IsAny<Guid>())).ReturnsAsync((CustomerActivationProcess?)null);

        var outbox = new Mock<IEventBusOutboxService>();
        var handler = CreateHandler(repo, outbox);

        await handler.Handle(
            new ActivationEmailRequestedDomainEvent(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()),
            CancellationToken.None);

        outbox.Verify(o => o.AddAndSaveEventAsync(
            It.IsAny<ActivationEmailRequestedIntegrationEvent>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenAdminEmailIsEmpty_DoesNotPublishEvent()
    {
        var process = CustomerActivationProcess.Create(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            companyLegalName: "Acme Corp",
            subscriptionPlanCode: "STANDARD",
            adminEmail: "");

        var repo = new Mock<ICustomerActivationProcessRepository>();
        repo.Setup(r => r.GetAsync(process.Id)).ReturnsAsync(process);

        var outbox = new Mock<IEventBusOutboxService>();
        var handler = CreateHandler(repo, outbox);

        await handler.Handle(
            new ActivationEmailRequestedDomainEvent(process.Id, process.AdminUserId, process.TenantId),
            CancellationToken.None);

        outbox.Verify(o => o.AddAndSaveEventAsync(
            It.IsAny<ActivationEmailRequestedIntegrationEvent>()), Times.Never);
    }

    private static ActivationEmailRequestedDomainEventHandler CreateHandler(
        Mock<ICustomerActivationProcessRepository> repo,
        Mock<IEventBusOutboxService> outbox) =>
        new(
            repo.Object,
            outbox.Object,
            new Mock<ILogger<ActivationEmailRequestedDomainEventHandler>>().Object);
}
