using Qlt.Platform.Messaging.IntegrationEvents;
using Qlt.Services.Accounts.Contracts.IntegrationEvents;

namespace Qlt.Infrastructure.Identity.IntegrationEvents.EventHandling;

/// <summary>
/// Handles company created integration events from Account service.
/// Legacy handler — tenant and user creation moved to dedicated handlers.
/// </summary>
public class CompanyCreatedIntegrationEventHandler : IIntegrationEventHandler<CompanyCreatedIntegrationEvent>
{
    private readonly ILogger<CompanyCreatedIntegrationEventHandler> _logger;

    public CompanyCreatedIntegrationEventHandler(ILogger<CompanyCreatedIntegrationEventHandler> logger)
    {
        _logger = logger;
    }

    public Task Handle(CompanyCreatedIntegrationEvent @event)
    {
        _logger.LogInformation(
            "Identity - Company created event received: {EventId} - {CompanyId} - {LegalName}",
            @event.Id,
            @event.CompanyId,
            @event.LegalName);

        return Task.CompletedTask;
    }
}
