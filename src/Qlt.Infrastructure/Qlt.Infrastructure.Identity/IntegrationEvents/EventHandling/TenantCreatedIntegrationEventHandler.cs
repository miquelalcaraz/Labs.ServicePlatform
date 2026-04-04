using Qlt.Infrastructure.Identity.Models;
using Qlt.Infrastructure.Identity.Repositories;
using Qlt.Platform.Messaging.IntegrationEvents;
using Qlt.Services.Accounts.Contracts.IntegrationEvents;

namespace Qlt.Infrastructure.Identity.IntegrationEvents.EventHandling;

/// <summary>
/// Creates a local Tenant projection in Identity when Account publishes a tenant created event.
/// </summary>
public class TenantCreatedIntegrationEventHandler : IIntegrationEventHandler<TenantCreatedIntegrationEvent>
{
    private readonly ILogger<TenantCreatedIntegrationEventHandler> _logger;
    private readonly ITenantRepository _tenantRepository;

    public TenantCreatedIntegrationEventHandler(
        ILogger<TenantCreatedIntegrationEventHandler> logger,
        ITenantRepository tenantRepository)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _tenantRepository = tenantRepository ?? throw new ArgumentNullException(nameof(tenantRepository));
    }

    public async Task Handle(TenantCreatedIntegrationEvent @event)
    {
        _logger.LogInformation(
            "Identity - Processing tenant created event: {EventId} - TenantId={TenantId}, Name={Name}",
            @event.Id,
            @event.TenantId,
            @event.Name);

        var existingTenant = await _tenantRepository.GetByIdAsync(@event.TenantId);
        if (existingTenant is not null)
        {
            _logger.LogInformation(
                "Identity - Tenant {TenantId} already exists locally. Skipping creation.",
                @event.TenantId);
            return;
        }

        var tenant = Tenant.Create(
            id: @event.TenantId,
            companyId: @event.OwnerCompanyId ?? Guid.Empty,
            name: @event.Name);

        await _tenantRepository.CreateAsync(tenant);

        _logger.LogInformation(
            "Identity - Local tenant {TenantId} created for company {CompanyId}",
            tenant.Id,
            @event.OwnerCompanyId);
    }
}
