using Microsoft.Extensions.Logging;

using Qlt.Contracts.Accounts.IntegrationEvents;
using Qlt.Platform.Messaging.Services;
using Qlt.Services.Accounts.Application.DomainEventHandlers;
using Qlt.Services.Accounts.Domain.Events;
using Qlt.Services.Accounts.Domain.Models.OnboardingAggregate;
using Qlt.Services.Accounts.Domain.Repositories;

namespace Services.UnitTests.Accounts.Application.DomainEventHandlers;

public class AdminProvisioningRequestedDomainEventHandlerTests
{
    [Fact]
    public async Task Handle_WhenOnboardingExists_PublishesCustomerAdminProvisioningRequestedIntegrationEvent()
    {
        var onboarding = OnboardingProcess.Create(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            companyLegalName: "Acme Corp",
            adminEmail: "admin@acme.com",
            adminFirstName: "John",
            adminLastName: "Doe",
            tenantName: "Acme Tenant",
            correlationId: Guid.NewGuid());
        onboarding.Start();
        onboarding.MarkSubscriptionCreated();
        onboarding.MarkTenantCreated();
        onboarding.MarkOwnerCompanyCreated(Guid.NewGuid());
        onboarding.MarkAdminProvisioningRequested();

        var repo = new Mock<IOnboardingProcessRepository>();
        repo.Setup(r => r.GetAsync(onboarding.Id)).ReturnsAsync(onboarding);

        var outbox = new Mock<IEventBusOutboxService>();
        var handler = CreateHandler(repo, outbox);

        await handler.Handle(
            new AdminProvisioningRequestedDomainEvent(onboarding.Id),
            CancellationToken.None);

        outbox.Verify(o => o.AddAndSaveEventAsync(
            It.Is<CustomerAdminProvisioningRequestedIntegrationEvent>(e =>
                e.SubscriptionId == onboarding.SubscriptionId &&
                e.TenantId == onboarding.TenantId &&
                e.CompanyId == onboarding.OwnerCompanyId &&
                e.OnboardingProcessId == onboarding.Id &&
                e.AdminEmail == "admin@acme.com" &&
                e.AdminFirstName == "John" &&
                e.AdminLastName == "Doe" &&
                e.TenantName == "Acme Tenant")),
            Times.Once);
    }

    [Fact]
    public async Task Handle_WhenOnboardingNotFound_DoesNotPublishEvent()
    {
        var repo = new Mock<IOnboardingProcessRepository>();
        repo.Setup(r => r.GetAsync(It.IsAny<Guid>())).ReturnsAsync((OnboardingProcess?)null);

        var outbox = new Mock<IEventBusOutboxService>();
        var handler = CreateHandler(repo, outbox);

        await handler.Handle(
            new AdminProvisioningRequestedDomainEvent(Guid.NewGuid()),
            CancellationToken.None);

        outbox.Verify(o => o.AddAndSaveEventAsync(
            It.IsAny<CustomerAdminProvisioningRequestedIntegrationEvent>()), Times.Never);
    }

    private static AdminProvisioningRequestedDomainEventHandler CreateHandler(
        Mock<IOnboardingProcessRepository> repo,
        Mock<IEventBusOutboxService> outbox) =>
        new(
            repo.Object,
            outbox.Object,
            new Mock<ILogger<AdminProvisioningRequestedDomainEventHandler>>().Object);
}
