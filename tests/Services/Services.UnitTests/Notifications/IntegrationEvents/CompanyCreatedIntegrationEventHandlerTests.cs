using MediatR;

using Microsoft.Extensions.Logging;

using Sirius.Contracts.Accounts.IntegrationEvents;
using Sirius.Platform;
using Sirius.Services.Notifications.Application.Commands;
using Sirius.Services.Notifications.Infrastructure.IntegrationEvents.EventHandling;

namespace Services.UnitTests.Notifications.IntegrationEvents;

public class CompanyCreatedIntegrationEventHandlerTests
{
    [Fact]
    public async Task Handle_SendsSendCompanyWelcomeEmailsCommand()
    {
        var companyId = Guid.NewGuid();
        var mediator = new Mock<IMediator>();
        mediator.Setup(m => m.Send(It.IsAny<SendCompanyWelcomeEmailsCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(true));

        var handler = CreateHandler(mediator);
        var @event = CreateEvent(companyId);

        await handler.Handle(@event);

        mediator.Verify(m => m.Send(
            It.Is<SendCompanyWelcomeEmailsCommand>(c =>
                c.CompanyId == companyId &&
                c.TradeName == "Acme" &&
                c.LegalName == "Acme Corp S.L." &&
                c.CountryCode == "ES"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    private static CompanyCreatedIntegrationEvent CreateEvent(Guid companyId) =>
        new(
            TenantId: Guid.NewGuid(),
            CompanyId: companyId,
            LegalName: "Acme Corp S.L.",
            TradeName: "Acme",
            TaxId: "B12345678",
            CountryCode: "ES");

    private static CompanyCreatedIntegrationEventHandler CreateHandler(
        Mock<IMediator>? mediator = null)
    {
        mediator ??= new Mock<IMediator>();
        return new(
            mediator.Object,
            new Mock<ILogger<CompanyCreatedIntegrationEventHandler>>().Object);
    }
}
