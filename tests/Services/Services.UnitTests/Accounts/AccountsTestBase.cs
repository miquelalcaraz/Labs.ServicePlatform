using Microsoft.Extensions.Logging;

using Miqa.Domain.SeedWork;
using Miqa.Platform.Messaging.Services;
using Miqa.Services.Accounts.Application.Commands;
using Miqa.Services.Accounts.Domain.Models.CompanyAggregate;
using Miqa.Services.Accounts.Domain.Models.OnboardingAggregate;
using Miqa.Services.Accounts.Domain.Models.SubscriptionAggregate;
using Miqa.Services.Accounts.Domain.Models.TenantAggregate;
using Miqa.Services.Accounts.Domain.Repositories;

namespace Services.UnitTests.Accounts;

public abstract class AccountsTestBase
{
    protected static readonly Guid InternalUserId = Guid.NewGuid();

    protected static StartCustomerOnboardingCommand CreateValidCommand(Guid? requestId = null) =>
        new(
            planCode: "STANDARD",
            companyLegalName: "Acme Corp S.L.",
            companyTradeName: "Acme",
            taxId: "B12345678",
            taxNumberType: TaxNumberType.Cif,
            country: "Spain",
            countryCode: "ES",
            adminEmail: "admin@acme.com",
            adminFirstName: "John",
            adminLastName: "Doe",
            createdByInternalUserId: InternalUserId,
            tenantName: "Acme Tenant",
            requestId: requestId ?? Guid.NewGuid());

    protected static (
        Mock<ITenantRepository> TenantRepo,
        Mock<ISubscriptionRepository> SubscriptionRepo,
        Mock<ICompanyRepository> CompanyRepo,
        Mock<IOnboardingProcessRepository> OnboardingRepo,
        Mock<IEventBusOutboxService> Outbox,
        Mock<IUnitOfWork> UnitOfWork)
        CreateRepositoryMocks(int saveChangesReturn = 1)
    {
        var unitOfWork = new Mock<IUnitOfWork>();
        unitOfWork
            .Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(saveChangesReturn);

        var tenantRepo = new Mock<ITenantRepository>();
        tenantRepo.Setup(r => r.UnitOfWork).Returns(unitOfWork.Object);
        tenantRepo.Setup(r => r.Add(It.IsAny<Tenant>())).Returns<Tenant>(t => t);

        var subscriptionRepo = new Mock<ISubscriptionRepository>();
        subscriptionRepo.Setup(r => r.Add(It.IsAny<Subscription>())).Returns<Subscription>(s => s);

        var companyRepo = new Mock<ICompanyRepository>();
        companyRepo.Setup(r => r.Add(It.IsAny<Company>())).Returns<Company>(c => c);

        var onboardingRepo = new Mock<IOnboardingProcessRepository>();
        onboardingRepo.Setup(r => r.Add(It.IsAny<OnboardingProcess>())).Returns<OnboardingProcess>(o => o);

        var outbox = new Mock<IEventBusOutboxService>();

        return (tenantRepo, subscriptionRepo, companyRepo, onboardingRepo, outbox, unitOfWork);
    }

    protected static Mock<ILogger<T>> CreateLogger<T>() => new();
}
