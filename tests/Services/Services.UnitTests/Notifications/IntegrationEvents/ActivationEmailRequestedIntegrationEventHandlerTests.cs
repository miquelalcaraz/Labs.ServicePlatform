using MediatR;

using Microsoft.Extensions.Logging;

using Qlt.Contracts.Accounts.IntegrationEvents;
using Qlt.Platform;
using Qlt.Services.Notifications.Application.Commands;
using Qlt.Services.Notifications.Infrastructure.IntegrationEvents.EventHandling;

namespace Services.UnitTests.Notifications.IntegrationEvents;

public class ActivationEmailRequestedIntegrationEventHandlerTests
{
    [Fact]
    public async Task Handle_SendsSendActivationEmailCommand()
    {
        var activationProcessId = Guid.NewGuid();
        var adminUserId = Guid.NewGuid();
        var correlationId = Guid.NewGuid();
        var mediator = new Mock<IMediator>();
        mediator.Setup(m => m.Send(It.IsAny<SendActivationEmailCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(true));

        var handler = CreateHandler(mediator);
        var @event = CreateValidEvent(activationProcessId, adminUserId, correlationId);

        await handler.Handle(@event);

        mediator.Verify(m => m.Send(
            It.Is<SendActivationEmailCommand>(c =>
                c.ActivationProcessId == activationProcessId &&
                c.AdminUserId == adminUserId &&
                c.AdminEmail == "admin@acme.com" &&
                c.CompanyLegalName == "Acme Corp S.L." &&
                c.CorrelationId == correlationId),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    private static ActivationEmailRequestedIntegrationEvent CreateValidEvent(
        Guid? activationProcessId = null,
        Guid? adminUserId = null,
        Guid? correlationId = null) =>
        new(
            ActivationProcessId: activationProcessId ?? Guid.NewGuid(),
            SubscriptionId: Guid.NewGuid(),
            TenantId: Guid.NewGuid(),
            OwnerCompanyId: Guid.NewGuid(),
            AdminUserId: adminUserId ?? Guid.NewGuid(),
            AdminEmail: "admin@acme.com",
            CompanyLegalName: "Acme Corp S.L.",
            SubscriptionPlanCode: "STANDARD",
            CorrelationId: correlationId ?? Guid.NewGuid());

    private static ActivationEmailRequestedIntegrationEventHandler CreateHandler(
        Mock<IMediator>? mediator = null)
    {
        mediator ??= new Mock<IMediator>();
        return new(
            mediator.Object,
            new Mock<ILogger<ActivationEmailRequestedIntegrationEventHandler>>().Object);
    }
}
