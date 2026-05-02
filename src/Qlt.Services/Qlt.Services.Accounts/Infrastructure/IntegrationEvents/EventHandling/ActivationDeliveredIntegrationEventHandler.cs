using MediatR;

using Microsoft.Extensions.Logging;

using Qlt.Contracts.Notifications.IntegrationEvents;
using Qlt.Platform.Messaging.IntegrationEvents;
using Qlt.Services.Accounts.Application.Commands;

namespace Qlt.Services.Accounts.Infrastructure.IntegrationEvents.EventHandling;

/// <summary>
/// Translates the ActivationDelivered integration event into an application command.
/// </summary>
public class ActivationDeliveredIntegrationEventHandler
    : IIntegrationEventHandler<ActivationDeliveredIntegrationEvent>
{
    private readonly IMediator _mediator;
    private readonly ILogger<ActivationDeliveredIntegrationEventHandler> _logger;

    public ActivationDeliveredIntegrationEventHandler(
        IMediator mediator,
        ILogger<ActivationDeliveredIntegrationEventHandler> logger)
    {
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task Handle(ActivationDeliveredIntegrationEvent @event)
    {
        _logger.LogInformation(
            "Received ActivationDelivered event for process {ActivationProcessId}.",
            @event.ActivationProcessId);

        var result = await _mediator.Send(
            new ConfirmActivationDeliveryCommand(@event.ActivationProcessId));

        if (result.IsFailure)
            _logger.LogWarning(
                "ConfirmActivationDelivery failed for process {ActivationProcessId}: {Error}",
                @event.ActivationProcessId,
                result.Error.Message);
    }
}
