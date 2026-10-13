using MediatR;

using Miqa.Contracts.Accounts.IntegrationEvents;
using Miqa.Infrastructure.Identity.Commands;
using Miqa.Platform.Messaging.IntegrationEvents;

namespace Miqa.Infrastructure.Identity.IntegrationEvents.EventHandling;

/// <summary>
/// Translates the CustomerAdminProvisioningRequested integration event into an application command.
/// </summary>
public class CustomerAdminProvisioningRequestedIntegrationEventHandler
    : IIntegrationEventHandler<CustomerAdminProvisioningRequestedIntegrationEvent>
{
    private readonly IMediator _mediator;
    private readonly ILogger<CustomerAdminProvisioningRequestedIntegrationEventHandler> _logger;

    public CustomerAdminProvisioningRequestedIntegrationEventHandler(
        IMediator mediator,
        ILogger<CustomerAdminProvisioningRequestedIntegrationEventHandler> logger)
    {
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task Handle(CustomerAdminProvisioningRequestedIntegrationEvent @event)
    {
        _logger.LogInformation(
            "Received CustomerAdminProvisioningRequested event for tenant {TenantId}, email={AdminEmail}.",
            @event.TenantId,
            @event.AdminEmail);

        var result = await _mediator.Send(
            new ProvisionCustomerAdminCommand(
                @event.SubscriptionId,
                @event.TenantId,
                @event.CompanyId,
                @event.OnboardingProcessId,
                @event.AdminEmail,
                @event.AdminFirstName,
                @event.AdminLastName,
                @event.TenantName));

        if (result.IsFailure)
            _logger.LogWarning(
                "ProvisionCustomerAdmin failed for tenant {TenantId}: {Error}",
                @event.TenantId,
                result.Error.Message);
    }
}
