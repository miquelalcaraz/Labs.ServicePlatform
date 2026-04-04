using Microsoft.Extensions.Logging;

using Qlt.Services.Accounts.Application.Commands;
using Qlt.Services.Accounts.Contracts.IntegrationEvents;
using Qlt.Services.Accounts.Domain.Models.CompanyAggregate;
using Qlt.Services.Accounts.Domain.Models.OnboardingAggregate;
using Qlt.Services.Accounts.Domain.Models.SubscriptionAggregate;
using Qlt.Services.Accounts.Domain.Models.TenantAggregate;

namespace Services.UnitTests.Accounts.Application.Commands;

public class StartCustomerOnboardingCommandHandlerTests : AccountsTestBase
{
    [Fact]
    public async Task Handle_WithValidCommand_ReturnsSuccessTrue()
    {
        var (tenantRepo, subscriptionRepo, companyRepo, onboardingRepo, outbox, _) = CreateRepositoryMocks();
        var handler = CreateHandler(tenantRepo, subscriptionRepo, companyRepo, onboardingRepo, outbox);

        var result = await handler.Handle(CreateValidCommand(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_WithValidCommand_AddsAllFourAggregates()
    {
        var (tenantRepo, subscriptionRepo, companyRepo, onboardingRepo, outbox, _) = CreateRepositoryMocks();
        var handler = CreateHandler(tenantRepo, subscriptionRepo, companyRepo, onboardingRepo, outbox);

        await handler.Handle(CreateValidCommand(), CancellationToken.None);

        tenantRepo.Verify(r => r.Add(It.IsAny<Tenant>()), Times.Once);
        subscriptionRepo.Verify(r => r.Add(It.IsAny<Subscription>()), Times.Once);
        companyRepo.Verify(r => r.Add(It.IsAny<Company>()), Times.Once);
        onboardingRepo.Verify(r => r.Add(It.IsAny<OnboardingProcess>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WithValidCommand_PublishesCustomerAdminProvisioningRequestedEvent()
    {
        var (tenantRepo, subscriptionRepo, companyRepo, onboardingRepo, outbox, _) = CreateRepositoryMocks();
        var handler = CreateHandler(tenantRepo, subscriptionRepo, companyRepo, onboardingRepo, outbox);
        var command = CreateValidCommand();

        await handler.Handle(command, CancellationToken.None);

        outbox.Verify(o => o.AddAndSaveEventAsync(
            It.Is<CustomerAdminProvisioningRequestedIntegrationEvent>(e =>
                e.AdminEmail == command.AdminEmail &&
                e.AdminFirstName == command.AdminFirstName &&
                e.AdminLastName == command.AdminLastName &&
                e.TenantName == command.TenantName)),
            Times.Once);
    }

    [Fact]
    public async Task Handle_WhenSaveChangesFails_ReturnsFailureAndDoesNotPublishEvent()
    {
        var (tenantRepo, subscriptionRepo, companyRepo, onboardingRepo, outbox, _) = CreateRepositoryMocks(saveChangesReturn: 0);
        var handler = CreateHandler(tenantRepo, subscriptionRepo, companyRepo, onboardingRepo, outbox);

        var result = await handler.Handle(CreateValidCommand(), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        outbox.Verify(o => o.AddAndSaveEventAsync(It.IsAny<CustomerAdminProvisioningRequestedIntegrationEvent>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenTenantNameIsNull_UsesFallbackToCompanyLegalName()
    {
        var (tenantRepo, subscriptionRepo, companyRepo, onboardingRepo, outbox, _) = CreateRepositoryMocks();
        var handler = CreateHandler(tenantRepo, subscriptionRepo, companyRepo, onboardingRepo, outbox);
        var command = new StartCustomerOnboardingCommand(
            planCode: "STANDARD",
            companyLegalName: "FallbackName Corp",
            companyTradeName: "FallbackName",
            taxId: "B12345678",
            taxNumberType: TaxNumberType.Cif,
            country: "Spain",
            countryCode: "ES",
            adminEmail: "admin@fallback.com",
            adminFirstName: "John",
            adminLastName: "Doe",
            createdByInternalUserId: InternalUserId,
            tenantName: null,
            requestId: Guid.NewGuid());

        await handler.Handle(command, CancellationToken.None);

        outbox.Verify(o => o.AddAndSaveEventAsync(
            It.Is<CustomerAdminProvisioningRequestedIntegrationEvent>(e =>
                e.TenantName == "FallbackName Corp")),
            Times.Once);
    }

    [Fact]
    public async Task Handle_PublishedEventContainsCorrectTenantAndCompanyIds()
    {
        Guid? capturedTenantId = null;
        Guid? capturedCompanyId = null;
        Guid? capturedSubscriptionId = null;

        var (tenantRepo, subscriptionRepo, companyRepo, onboardingRepo, outbox, _) = CreateRepositoryMocks();

        outbox
            .Setup(o => o.AddAndSaveEventAsync(It.IsAny<CustomerAdminProvisioningRequestedIntegrationEvent>()))
            .Callback<Qlt.Platform.Messaging.IntegrationEvents.IntegrationEvent>(e =>
            {
                var evt = (CustomerAdminProvisioningRequestedIntegrationEvent)e;
                capturedTenantId = evt.TenantId;
                capturedCompanyId = evt.CompanyId;
                capturedSubscriptionId = evt.SubscriptionId;
            })
            .Returns(Task.CompletedTask);

        var handler = CreateHandler(tenantRepo, subscriptionRepo, companyRepo, onboardingRepo, outbox);
        await handler.Handle(CreateValidCommand(), CancellationToken.None);

        capturedTenantId.Should().NotBeEmpty();
        capturedCompanyId.Should().NotBeEmpty();
        capturedSubscriptionId.Should().NotBeEmpty();
    }

    private static StartCustomerOnboardingCommandHandler CreateHandler(
        Mock<Qlt.Services.Accounts.Domain.Repositories.ITenantRepository> tenantRepo,
        Mock<Qlt.Services.Accounts.Domain.Repositories.ISubscriptionRepository> subscriptionRepo,
        Mock<Qlt.Services.Accounts.Domain.Repositories.ICompanyRepository> companyRepo,
        Mock<Qlt.Services.Accounts.Domain.Repositories.IOnboardingProcessRepository> onboardingRepo,
        Mock<Qlt.Platform.Messaging.Services.IEventBusOutboxService> outbox) =>
        new(
            tenantRepo.Object,
            subscriptionRepo.Object,
            companyRepo.Object,
            onboardingRepo.Object,
            outbox.Object,
            new Mock<ILogger<StartCustomerOnboardingCommandHandler>>().Object);
}
