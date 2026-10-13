using Microsoft.Extensions.Logging;

using Miqa.Contracts.Accounts.IntegrationEvents;
using Miqa.Platform.Messaging.DomainEvents;
using Miqa.Platform.Messaging.Services;

using Miqa.Services.Accounts.Domain.Events;
using Miqa.Services.Accounts.Domain.Repositories;

namespace Miqa.Services.Accounts.Application.DomainEventHandlers;

/// <summary>
/// Handles the TenantCreatedDomainEvent by publishing an integration event
/// to notify other bounded contexts about the new tenant.
/// </summary>
public class TenantCreatedDomainEventHandler : IDomainEventHandler<TenantCreatedDomainEvent>
{
    private readonly ILogger<TenantCreatedDomainEventHandler> _logger;
    private readonly ITenantRepository _tenantRepository;
    private readonly IEventBusOutboxService _outboxService;

    public TenantCreatedDomainEventHandler(
        ILogger<TenantCreatedDomainEventHandler> logger,
        ITenantRepository tenantRepository,
        IEventBusOutboxService outboxService)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _tenantRepository = tenantRepository ?? throw new ArgumentNullException(nameof(tenantRepository));
        _outboxService = outboxService ?? throw new ArgumentNullException(nameof(outboxService));
    }

    public async Task Handle(TenantCreatedDomainEvent notification, CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Tenant created: {TenantId} - {Name}",
            notification.TenantId,
            notification.Name);

        var tenant = await _tenantRepository.GetAsync(notification.TenantId);

        if (tenant is not null)
        {
            var integrationEvent = new TenantCreatedIntegrationEvent(
                tenant.Id,
                tenant.Name,
                tenant.OwnerCompanyId,
                tenant.CreatedByInternalUserId);

            await _outboxService.AddAndSaveEventAsync(integrationEvent);
        }
        else
        {
            _logger.LogWarning(
                "Tenant {TenantId} not found in repository during domain event handling. Integration event not published.",
                notification.TenantId);
        }
    }
}
