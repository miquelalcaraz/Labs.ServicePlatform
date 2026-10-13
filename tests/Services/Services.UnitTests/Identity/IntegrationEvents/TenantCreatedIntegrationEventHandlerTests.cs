using MediatR;

using Microsoft.Extensions.Logging;

using Miqa.Contracts.Accounts.IntegrationEvents;
using Miqa.Infrastructure.Identity.Commands;
using Miqa.Infrastructure.Identity.IntegrationEvents.EventHandling;
using Miqa.Platform;

namespace Services.UnitTests.Identity.IntegrationEvents;

public class TenantCreatedIntegrationEventHandlerTests : IdentityTestBase
{
    [Fact]
    public async Task Handle_SendsCreateLocalTenantCommand()
    {
        var mediator = new Mock<IMediator>();
        mediator.Setup(m => m.Send(It.IsAny<CreateLocalTenantCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(true));

        var handler = new TenantCreatedIntegrationEventHandler(
            mediator.Object,
            new Mock<ILogger<TenantCreatedIntegrationEventHandler>>().Object);

        var @event = CreateValidTenantCreatedEvent(tenantId: TenantId, ownerCompanyId: CompanyId);

        await handler.Handle(@event);

        mediator.Verify(m => m.Send(
            It.Is<CreateLocalTenantCommand>(c =>
                c.TenantId == @event.TenantId &&
                c.Name == @event.Name &&
                c.OwnerCompanyId == CompanyId),
            It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_WhenOwnerCompanyIdIsNull_SendsCommandWithEmptyGuid()
    {
        var mediator = new Mock<IMediator>();
        mediator.Setup(m => m.Send(It.IsAny<CreateLocalTenantCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(true));

        var handler = new TenantCreatedIntegrationEventHandler(
            mediator.Object,
            new Mock<ILogger<TenantCreatedIntegrationEventHandler>>().Object);

        var @event = new TenantCreatedIntegrationEvent(
            TenantId: Guid.NewGuid(),
            Name: "Acme Tenant",
            OwnerCompanyId: null,
            CreatedByInternalUserId: Guid.NewGuid());

        await handler.Handle(@event);

        mediator.Verify(m => m.Send(
            It.Is<CreateLocalTenantCommand>(c => c.OwnerCompanyId == Guid.Empty),
            It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_WhenCommandFails_LogsWarning()
    {
        var mediator = new Mock<IMediator>();
        mediator.Setup(m => m.Send(It.IsAny<CreateLocalTenantCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure<bool>(Error.NotFound.WithMessage("Not found")));

        var logger = new Mock<ILogger<TenantCreatedIntegrationEventHandler>>();

        var handler = new TenantCreatedIntegrationEventHandler(
            mediator.Object,
            logger.Object);

        var @event = CreateValidTenantCreatedEvent();

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
