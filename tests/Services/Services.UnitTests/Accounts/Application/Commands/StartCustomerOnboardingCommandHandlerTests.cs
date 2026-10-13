using Microsoft.Extensions.Logging;

using Sirius.Services.Accounts.Application.Commands;
using Sirius.Services.Accounts.Domain.Events;
using Sirius.Services.Accounts.Domain.Models.CompanyAggregate;
using Sirius.Services.Accounts.Domain.Models.OnboardingAggregate;
using Sirius.Services.Accounts.Domain.Models.SubscriptionAggregate;
using Sirius.Services.Accounts.Domain.Models.TenantAggregate;

namespace Services.UnitTests.Accounts.Application.Commands;

public class StartCustomerOnboardingCommandHandlerTests : AccountsTestBase
{
    [Fact]
    public async Task Handle_WithValidCommand_ReturnsSuccessTrue()
    {
        var (tenantRepo, subscriptionRepo, companyRepo, onboardingRepo, _, _) = CreateRepositoryMocks();
        var handler = CreateHandler(tenantRepo, subscriptionRepo, companyRepo, onboardingRepo);

        var result = await handler.Handle(CreateValidCommand(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_WithValidCommand_AddsAllFourAggregates()
    {
        var (tenantRepo, subscriptionRepo, companyRepo, onboardingRepo, _, _) = CreateRepositoryMocks();
        var handler = CreateHandler(tenantRepo, subscriptionRepo, companyRepo, onboardingRepo);

        await handler.Handle(CreateValidCommand(), CancellationToken.None);

        tenantRepo.Verify(r => r.Add(It.IsAny<Tenant>()), Times.Once);
        subscriptionRepo.Verify(r => r.Add(It.IsAny<Subscription>()), Times.Once);
        companyRepo.Verify(r => r.Add(It.IsAny<Company>()), Times.Once);
        onboardingRepo.Verify(r => r.Add(It.IsAny<OnboardingProcess>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WithValidCommand_RaisesAdminProvisioningRequestedDomainEvent()
    {
        OnboardingProcess? capturedOnboarding = null;
        var (tenantRepo, subscriptionRepo, companyRepo, onboardingRepo, _, _) = CreateRepositoryMocks();
        onboardingRepo
            .Setup(r => r.Add(It.IsAny<OnboardingProcess>()))
            .Callback<OnboardingProcess>(o => capturedOnboarding = o);

        var handler = CreateHandler(tenantRepo, subscriptionRepo, companyRepo, onboardingRepo);
        var command = CreateValidCommand();

        await handler.Handle(command, CancellationToken.None);

        capturedOnboarding.Should().NotBeNull();
        capturedOnboarding!.DomainEvents.Should().ContainSingle(e => e is AdminProvisioningRequestedDomainEvent);
    }

    [Fact]
    public async Task Handle_WhenSaveChangesFails_ReturnsFailure()
    {
        var (tenantRepo, subscriptionRepo, companyRepo, onboardingRepo, _, _) = CreateRepositoryMocks(saveChangesReturn: 0);
        var handler = CreateHandler(tenantRepo, subscriptionRepo, companyRepo, onboardingRepo);

        var result = await handler.Handle(CreateValidCommand(), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_WhenTenantNameIsNull_UsesFallbackToCompanyLegalName()
    {
        Tenant? capturedTenant = null;
        var (tenantRepo, subscriptionRepo, companyRepo, onboardingRepo, _, _) = CreateRepositoryMocks();
        tenantRepo
            .Setup(r => r.Add(It.IsAny<Tenant>()))
            .Callback<Tenant>(t => capturedTenant = t);

        var handler = CreateHandler(tenantRepo, subscriptionRepo, companyRepo, onboardingRepo);
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

        capturedTenant.Should().NotBeNull();
        capturedTenant!.Name.Should().Be("FallbackName Corp");
    }

    private static StartCustomerOnboardingCommandHandler CreateHandler(
        Mock<Sirius.Services.Accounts.Domain.Repositories.ITenantRepository> tenantRepo,
        Mock<Sirius.Services.Accounts.Domain.Repositories.ISubscriptionRepository> subscriptionRepo,
        Mock<Sirius.Services.Accounts.Domain.Repositories.ICompanyRepository> companyRepo,
        Mock<Sirius.Services.Accounts.Domain.Repositories.IOnboardingProcessRepository> onboardingRepo) =>
        new(
            tenantRepo.Object,
            subscriptionRepo.Object,
            companyRepo.Object,
            onboardingRepo.Object,
            new Mock<ILogger<StartCustomerOnboardingCommandHandler>>().Object);
}
