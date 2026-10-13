using MediatR;

using Microsoft.Extensions.Logging;

using Miqa.Infrastructure.Identity.Commands;
using Miqa.Infrastructure.Identity.IntegrationEvents.EventHandling;
using Miqa.Platform;

namespace Services.UnitTests.Identity.IntegrationEvents;

public class CustomerAdminProvisioningRequestedIntegrationEventHandlerTests : IdentityTestBase
{
    [Fact]
    public async Task Handle_SendsProvisionCustomerAdminCommand()
    {
        var mediator = new Mock<IMediator>();
        mediator.Setup(m => m.Send(It.IsAny<ProvisionCustomerAdminCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(true));

        var handler = new CustomerAdminProvisioningRequestedIntegrationEventHandler(
            mediator.Object,
            new Mock<ILogger<CustomerAdminProvisioningRequestedIntegrationEventHandler>>().Object);

        var @event = CreateValidProvisioningEvent(tenantId: TenantId, companyId: CompanyId);

        await handler.Handle(@event);

        mediator.Verify(m => m.Send(
            It.Is<ProvisionCustomerAdminCommand>(c =>
                c.SubscriptionId == @event.SubscriptionId &&
                c.TenantId == @event.TenantId &&
                c.CompanyId == @event.CompanyId &&
                c.OnboardingProcessId == @event.OnboardingProcessId &&
                c.AdminEmail == @event.AdminEmail &&
                c.AdminFirstName == @event.AdminFirstName &&
                c.AdminLastName == @event.AdminLastName &&
                c.TenantName == @event.TenantName),
            It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_WhenCommandFails_LogsWarning()
    {
        var mediator = new Mock<IMediator>();
        mediator.Setup(m => m.Send(It.IsAny<ProvisionCustomerAdminCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure<bool>(Error.NotFound.WithMessage("Not found")));

        var logger = new Mock<ILogger<CustomerAdminProvisioningRequestedIntegrationEventHandler>>();

        var handler = new CustomerAdminProvisioningRequestedIntegrationEventHandler(
            mediator.Object,
            logger.Object);

        var @event = CreateValidProvisioningEvent();

        await handler.Handle(@event);

        logger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }
}
