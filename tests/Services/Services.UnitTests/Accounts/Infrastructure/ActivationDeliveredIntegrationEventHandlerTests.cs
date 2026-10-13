using MediatR;

using Microsoft.Extensions.Logging;

using Miqa.Contracts.Notifications.IntegrationEvents;
using Miqa.Platform;
using Miqa.Services.Accounts.Application.Commands;
using Miqa.Services.Accounts.Infrastructure.IntegrationEvents.EventHandling;

namespace Services.UnitTests.Accounts.Infrastructure;

public class ActivationDeliveredIntegrationEventHandlerTests
{
    [Fact]
    public async Task Handle_SendsConfirmActivationDeliveryCommand()
    {
        var activationProcessId = Guid.NewGuid();
        var mediator = new Mock<IMediator>();
        mediator.Setup(m => m.Send(It.IsAny<ConfirmActivationDeliveryCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(true));

        var handler = CreateHandler(mediator);

        await handler.Handle(CreateEvent(activationProcessId));

        mediator.Verify(m => m.Send(
            It.Is<ConfirmActivationDeliveryCommand>(c => c.ActivationProcessId == activationProcessId),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    private static ActivationDeliveredIntegrationEvent CreateEvent(Guid activationProcessId) =>
        new(
            ActivationProcessId: activationProcessId,
            UserId: Guid.NewGuid(),
            Email: "admin@acme.com",
            DeliveredAtUtc: DateTimeOffset.UtcNow,
            CorrelationId: Guid.NewGuid());

    private static ActivationDeliveredIntegrationEventHandler CreateHandler(
        Mock<IMediator>? mediator = null)
    {
        mediator ??= new Mock<IMediator>();
        return new(
            mediator.Object,
            new Mock<ILogger<ActivationDeliveredIntegrationEventHandler>>().Object);
    }
}
