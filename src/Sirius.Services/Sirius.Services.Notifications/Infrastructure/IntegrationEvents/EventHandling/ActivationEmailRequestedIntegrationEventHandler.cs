using MediatR;

using Microsoft.Extensions.Logging;

using Sirius.Contracts.Accounts.IntegrationEvents;
using Sirius.Platform.Messaging.IntegrationEvents;
using Sirius.Services.Notifications.Application.Commands;

namespace Sirius.Services.Notifications.Infrastructure.IntegrationEvents.EventHandling;

/// <summary>
/// Translates the ActivationEmailRequested integration event into an application command.
/// </summary>
public class ActivationEmailRequestedIntegrationEventHandler
    : IIntegrationEventHandler<ActivationEmailRequestedIntegrationEvent>
{
    private readonly IMediator _mediator;
    private readonly ILogger<ActivationEmailRequestedIntegrationEventHandler> _logger;

    public ActivationEmailRequestedIntegrationEventHandler(
        IMediator mediator,
        ILogger<ActivationEmailRequestedIntegrationEventHandler> logger)
    {
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task Handle(ActivationEmailRequestedIntegrationEvent @event)
    {
        _logger.LogInformation(
            "Received ActivationEmailRequested event for ActivationProcessId={ActivationProcessId}.",
            @event.ActivationProcessId);

        var result = await _mediator.Send(
            new SendActivationEmailCommand(
                @event.ActivationProcessId,
                @event.AdminUserId,
                @event.AdminEmail,
                @event.CompanyLegalName,
                @event.CorrelationId));

        if (result.IsFailure)
            _logger.LogWarning(
                "SendActivationEmail failed for ActivationProcessId={ActivationProcessId}: {Error}",
                @event.ActivationProcessId,
                result.Error.Message);
    }
}
