using Microsoft.Extensions.Logging;

using Qlt.Contracts.Accounts.IntegrationEvents;
using Qlt.Platform.Messaging.Services;
using Qlt.Services.Accounts.Application.DomainEventHandlers;
using Qlt.Services.Accounts.Domain.Events;
using Qlt.Services.Accounts.Domain.Models.CompanyAggregate;
using Qlt.Services.Accounts.Domain.Repositories;

namespace Services.UnitTests.Accounts.Application.DomainEventHandlers;

public class CompanyCreatedDomainEventHandlerTests
{
    [Fact]
    public async Task Handle_WhenCompanyExists_PublishesCompanyCreatedIntegrationEvent()
    {
        var tenantId = Guid.NewGuid();
        var company = Company.CreateOwnerCompany(
            tenantId,
            legalName: "Acme Corp S.L.",
            tradeName: "Acme",
            address: Address.Create("Spain", "ES", null, "28001", "Madrid", "Main St 1"),
            taxId: "B12345678",
            taxNumberType: TaxNumberType.Cif,
            createdByInternalUserId: Guid.NewGuid());

        var companyRepo = new Mock<ICompanyRepository>();
        companyRepo.Setup(r => r.GetAsync(company.Id)).ReturnsAsync(company);

        var outbox = new Mock<IEventBusOutboxService>();
        var handler = CreateHandler(companyRepo, outbox);

        await handler.Handle(
            new CompanyCreatedDomainEvent(tenantId, company.Id, "Acme Corp S.L.", "Acme", "B12345678", "ES"),
            CancellationToken.None);

        outbox.Verify(o => o.AddAndSaveEventAsync(
            It.Is<CompanyCreatedIntegrationEvent>(e =>
                e.TenantId == tenantId &&
                e.CompanyId == company.Id &&
                e.LegalName == "Acme Corp S.L." &&
                e.TradeName == "Acme" &&
                e.TaxId == "B12345678" &&
                e.CountryCode == "ES")),
            Times.Once);
    }

    [Fact]
    public async Task Handle_WhenCompanyNotFound_DoesNotPublishEvent()
    {
        var companyRepo = new Mock<ICompanyRepository>();
        companyRepo.Setup(r => r.GetAsync(It.IsAny<Guid>())).ReturnsAsync((Company?)null);

        var outbox = new Mock<IEventBusOutboxService>();
        var handler = CreateHandler(companyRepo, outbox);

        await handler.Handle(
            new CompanyCreatedDomainEvent(Guid.NewGuid(), Guid.NewGuid(), "Ghost", "Ghost", "X000", "XX"),
            CancellationToken.None);

        outbox.Verify(o => o.AddAndSaveEventAsync(
            It.IsAny<CompanyCreatedIntegrationEvent>()), Times.Never);
    }

    private static CompanyCreatedDomainEventHandler CreateHandler(
        Mock<ICompanyRepository> companyRepo,
        Mock<IEventBusOutboxService> outbox) =>
        new(
            new Mock<ILogger<CompanyCreatedDomainEventHandler>>().Object,
            companyRepo.Object,
            outbox.Object);
}
