using MediatR;

using Microsoft.Extensions.Logging;

using Sirius.Contracts.Identity.IntegrationEvents;
using Sirius.Platform;
using Sirius.Services.Accounts.Application.Commands;
using Sirius.Services.Accounts.Infrastructure.IntegrationEvents.EventHandling;

namespace Services.UnitTests.Accounts.Infrastructure;

public class ExternalUserProvisionedIntegrationEventHandlerTests
{
    [Fact]
    public async Task Handle_SendsCompleteOnboardingCommand()
    {
        var mediator = new Mock<IMediator>();
        mediator.Setup(m => m.Send(It.IsAny<CompleteOnboardingCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(new CompleteOnboardingResult(Guid.NewGuid(), "Acme Corp", "starter")));
        mediator.Setup(m => m.Send(It.IsAny<StartCustomerActivationProcessCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(true));

        var handler = CreateHandler(mediator);
        var @event = CreateProvisioningEvent();

        await handler.Handle(@event);

        mediator.Verify(m => m.Send(
            It.Is<CompleteOnboardingCommand>(c =>
                c.OnboardingProcessId == @event.OnboardingProcessId &&
                c.UserId == @event.UserId &&
                c.TenantId == @event.TenantId &&
                c.CompanyId == @event.CompanyId),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenOnboardingCompletes_SendsStartActivationCommandWithOnboardingResult()
    {
        var membershipId = Guid.NewGuid();
        var mediator = new Mock<IMediator>();
        mediator.Setup(m => m.Send(It.IsAny<CompleteOnboardingCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(new CompleteOnboardingResult(membershipId, "Acme Corp", "starter")));
        mediator.Setup(m => m.Send(It.IsAny<StartCustomerActivationProcessCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(true));

        var handler = CreateHandler(mediator);
        var @event = CreateProvisioningEvent();

        await handler.Handle(@event);

        mediator.Verify(m => m.Send(
            It.Is<StartCustomerActivationProcessCommand>(c =>
                c.SubscriptionId == @event.SubscriptionId &&
                c.AdminUserId == @event.UserId &&
                c.AdminMembershipId == membershipId &&
                c.CompanyLegalName == "Acme Corp" &&
                c.SubscriptionPlanCode == "starter" &&
                c.RequestId == @event.OnboardingProcessId),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenCompleteOnboardingFails_DoesNotSendActivationCommand()
    {
        var mediator = new Mock<IMediator>();
        mediator.Setup(m => m.Send(It.IsAny<CompleteOnboardingCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure<CompleteOnboardingResult>(Error.NotFound));

        var handler = CreateHandler(mediator);

        await handler.Handle(CreateProvisioningEvent());

        mediator.Verify(m => m.Send(It.IsAny<StartCustomerActivationProcessCommand>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private static ExternalUserProvisionedIntegrationEvent CreateProvisioningEvent() =>
        new(
            UserId: Guid.NewGuid(),
            TenantId: Guid.NewGuid(),
            CompanyId: Guid.NewGuid(),
            SubscriptionId: Guid.NewGuid(),
            OnboardingProcessId: Guid.NewGuid(),
            Email: "admin@acme.com",
            FirstName: "John",
            LastName: "Doe");

    private static ExternalUserProvisionedIntegrationEventHandler CreateHandler(
        Mock<IMediator>? mediator = null)
    {
        mediator ??= new Mock<IMediator>();
        return new(
            mediator.Object,
            new Mock<ILogger<ExternalUserProvisionedIntegrationEventHandler>>().Object);
    }
}
