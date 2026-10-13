using MediatR;

using Microsoft.Extensions.Logging;

using Sirius.Contracts.Accounts.IntegrationEvents;
using Sirius.Platform.Messaging.IntegrationEvents;
using Sirius.Services.Notifications.Application.Commands;

namespace Sirius.Services.Notifications.Infrastructure.IntegrationEvents.EventHandling;

/// <summary>
/// Translates the CompanyCreated integration event into an application command.
/// </summary>
public class CompanyCreatedIntegrationEventHandler : IIntegrationEventHandler<CompanyCreatedIntegrationEvent>
{
    private readonly IMediator _mediator;
    private readonly ILogger<CompanyCreatedIntegrationEventHandler> _logger;

    public CompanyCreatedIntegrationEventHandler(
        IMediator mediator,
        ILogger<CompanyCreatedIntegrationEventHandler> logger)
    {
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task Handle(CompanyCreatedIntegrationEvent @event)
    {
        _logger.LogInformation(
            "Received CompanyCreated event for CompanyId={CompanyId}.",
            @event.CompanyId);

        var result = await _mediator.Send(
            new SendCompanyWelcomeEmailsCommand(
                @event.CompanyId,
                @event.TradeName,
                @event.LegalName,
                @event.CountryCode));

        if (result.IsFailure)
            _logger.LogWarning(
                "SendCompanyWelcomeEmails failed for CompanyId={CompanyId}: {Error}",
                @event.CompanyId,
                result.Error.Message);
    }
}
