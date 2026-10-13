using MediatR;

using Miqa.Contracts.Accounts.IntegrationEvents;
using Miqa.Infrastructure.Identity.Commands;
using Miqa.Platform.Messaging.IntegrationEvents;

namespace Miqa.Infrastructure.Identity.IntegrationEvents.EventHandling;

/// <summary>
/// Translates the TenantCreated integration event into an application command.
/// </summary>
public class TenantCreatedIntegrationEventHandler : IIntegrationEventHandler<TenantCreatedIntegrationEvent>
{
    private readonly IMediator _mediator;
    private readonly ILogger<TenantCreatedIntegrationEventHandler> _logger;

    public TenantCreatedIntegrationEventHandler(
        IMediator mediator,
        ILogger<TenantCreatedIntegrationEventHandler> logger)
    {
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task Handle(TenantCreatedIntegrationEvent @event)
    {
        _logger.LogInformation(
            "Received TenantCreated event: {EventId} - TenantId={TenantId}, Name={Name}.",
            @event.Id,
            @event.TenantId,
            @event.Name);

        var result = await _mediator.Send(
            new CreateLocalTenantCommand(
                @event.TenantId,
                @event.Name,
                @event.OwnerCompanyId ?? Guid.Empty));

        if (result.IsFailure)
            _logger.LogWarning(
                "CreateLocalTenant failed for tenant {TenantId}: {Error}",
                @event.TenantId,
                result.Error.Message);
    }
}
