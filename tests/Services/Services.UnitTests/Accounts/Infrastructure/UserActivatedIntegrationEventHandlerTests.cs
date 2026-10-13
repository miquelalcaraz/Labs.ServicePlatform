using MediatR;

using Microsoft.Extensions.Logging;

using Sirius.Contracts.Identity.IntegrationEvents;
using Sirius.Platform;
using Sirius.Services.Accounts.Application.Commands;
using Sirius.Services.Accounts.Infrastructure.IntegrationEvents.EventHandling;

namespace Services.UnitTests.Accounts.Infrastructure;

public class UserActivatedIntegrationEventHandlerTests
{
    private static readonly Guid AdminUserId = Guid.NewGuid();

    [Fact]
    public async Task Handle_SendsCompleteActivationOnUserActivatedCommand()
    {
        var mediator = new Mock<IMediator>();
        mediator.Setup(m => m.Send(It.IsAny<CompleteActivationOnUserActivatedCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(true));

        var handler = CreateHandler(mediator);

        await handler.Handle(CreateEvent(AdminUserId));

        mediator.Verify(m => m.Send(
            It.Is<CompleteActivationOnUserActivatedCommand>(c => c.UserId == AdminUserId),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    private static UserActivatedIntegrationEvent CreateEvent(Guid userId) =>
        new(
            UserId: userId,
            Email: "admin@acme.com",
            TenantId: Guid.NewGuid(),
            ActivatedAtUtc: DateTimeOffset.UtcNow,
            CorrelationId: Guid.NewGuid());

    private static UserActivatedIntegrationEventHandler CreateHandler(
        Mock<IMediator>? mediator = null)
    {
        mediator ??= new Mock<IMediator>();
        return new(
            mediator.Object,
            new Mock<ILogger<UserActivatedIntegrationEventHandler>>().Object);
    }
}
