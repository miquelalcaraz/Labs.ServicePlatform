using MediatR;

using Microsoft.Extensions.Logging;

using Miqa.Contracts.Identity.IntegrationEvents;
using Miqa.Platform.Messaging.IntegrationEvents;
using Miqa.Services.Accounts.Application.Commands;

namespace Miqa.Services.Accounts.Infrastructure.IntegrationEvents.EventHandling;

/// <summary>
/// Translates the UserActivated integration event into an application command.
/// </summary>
public class UserActivatedIntegrationEventHandler
    : IIntegrationEventHandler<UserActivatedIntegrationEvent>
{
    private readonly IMediator _mediator;
    private readonly ILogger<UserActivatedIntegrationEventHandler> _logger;

    public UserActivatedIntegrationEventHandler(
        IMediator mediator,
        ILogger<UserActivatedIntegrationEventHandler> logger)
    {
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task Handle(UserActivatedIntegrationEvent @event)
    {
        _logger.LogInformation(
            "Received UserActivated event for UserId={UserId}.",
            @event.UserId);

        var result = await _mediator.Send(
            new CompleteActivationOnUserActivatedCommand(@event.UserId));

        if (result.IsFailure)
            _logger.LogWarning(
                "CompleteActivationOnUserActivated failed for UserId={UserId}: {Error}",
                @event.UserId,
                result.Error.Message);
    }
}
