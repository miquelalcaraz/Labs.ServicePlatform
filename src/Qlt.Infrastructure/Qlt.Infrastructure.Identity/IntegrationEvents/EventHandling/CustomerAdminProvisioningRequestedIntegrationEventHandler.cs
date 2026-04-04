using global::Qlt.Infrastructure.Identity.Contracts.IntegrationEvents;
using Qlt.Infrastructure.Identity.Models;
using Qlt.Infrastructure.Identity.Repositories;
using Qlt.Platform.Messaging.IntegrationEvents;
using Qlt.Platform.Messaging.Services;
using Qlt.Services.Accounts.Contracts.IntegrationEvents;

namespace Qlt.Infrastructure.Identity.IntegrationEvents.EventHandling;

/// <summary>
/// Provisions the customer admin user in Identity when requested by Account service.
/// Publishes UserProvisionedIntegrationEvent upon successful user creation.
/// </summary>
public class CustomerAdminProvisioningRequestedIntegrationEventHandler
    : IIntegrationEventHandler<CustomerAdminProvisioningRequestedIntegrationEvent>
{
    private readonly ILogger<CustomerAdminProvisioningRequestedIntegrationEventHandler> _logger;
    private readonly ITenantRepository _tenantRepository;
    private readonly IUserRepository _userRepository;
    private readonly IEventBusOutboxService _outboxService;

    public CustomerAdminProvisioningRequestedIntegrationEventHandler(
        ILogger<CustomerAdminProvisioningRequestedIntegrationEventHandler> logger,
        ITenantRepository tenantRepository,
        IUserRepository userRepository,
        IEventBusOutboxService outboxService)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _tenantRepository = tenantRepository ?? throw new ArgumentNullException(nameof(tenantRepository));
        _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
        _outboxService = outboxService ?? throw new ArgumentNullException(nameof(outboxService));
    }

    public async Task Handle(CustomerAdminProvisioningRequestedIntegrationEvent @event)
    {
        _logger.LogInformation(
            "Identity - Provisioning admin user for tenant {TenantId}, email={AdminEmail}",
            @event.TenantId,
            @event.AdminEmail);

        await EnsureTenantExistsAsync(@event);

        var adminUser = await CreateAdminUserIfNotExistsAsync(@event);

        await _outboxService.AddAndSaveEventAsync(
            new ExternalUserProvisionedIntegrationEvent(
                Guid.Parse(adminUser.Id),
                @event.TenantId,
                @event.CompanyId,
                @event.SubscriptionId,
                @event.OnboardingProcessId,
                @event.AdminEmail,
                @event.AdminFirstName,
                @event.AdminLastName));

        _logger.LogInformation(
            "Identity - Admin user {UserId} provisioned for tenant {TenantId}. Published ExternalUserProvisionedIntegrationEvent.",
            adminUser.Id,
            @event.TenantId);
    }

    private async Task EnsureTenantExistsAsync(CustomerAdminProvisioningRequestedIntegrationEvent @event)
    {
        var tenant = await _tenantRepository.GetByIdAsync(@event.TenantId);
        if (tenant is not null)
            return;

        _logger.LogInformation(
            "Identity - Tenant {TenantId} not found locally. Creating from provisioning event.",
            @event.TenantId);

        var newTenant = Tenant.Create(
            id: @event.TenantId,
            companyId: @event.CompanyId,
            name: @event.TenantName);

        await _tenantRepository.CreateAsync(newTenant);
    }

    private async Task<ApplicationUser> CreateAdminUserIfNotExistsAsync(
        CustomerAdminProvisioningRequestedIntegrationEvent @event)
    {
        var existingUser = await _userRepository.GetByEmailAsync(@event.AdminEmail);
        if (existingUser is not null)
        {
            _logger.LogInformation(
                "Identity - Admin user with email {AdminEmail} already exists (UserId={UserId}). Skipping creation.",
                @event.AdminEmail,
                existingUser.Id);
            return existingUser;
        }

        var adminUser = ApplicationUser.CreateAsUnConfirmed(
            userName: @event.AdminEmail,
            email: @event.AdminEmail,
            tenantId: @event.TenantId,
            firstName: @event.AdminFirstName,
            lastName: @event.AdminLastName);

        return await _userRepository.CreateAsync(adminUser);
    }
}
