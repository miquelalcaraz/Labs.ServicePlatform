using Qlt.Infrastructure.Identity.Models;
using Qlt.Infrastructure.Identity.Repositories;
using Qlt.Infrastructure.Identity.Services;
using Qlt.Platform.Messaging.IntegrationEvents;
using Qlt.Services.Accounts.Contracts.IntegrationEvents;

namespace Qlt.Infrastructure.Identity.IntegrationEvents.EventHandling
{
    /// <summary>
    /// Handles organization created integration events.
    /// Telemetry and idempotency are automatically enforced by decorators.
    /// </summary>
    public class OrganizationCreatedIntegrationEventHandler : IIntegrationEventHandler<OrganizationCreatedIntegrationEvent>
    {
        private readonly ILogger<OrganizationCreatedIntegrationEventHandler> _logger;
        private readonly ITenantRepository _tenantRepository;
        private readonly IUserRepository _userRepository;
        private readonly IPasswordGenerator _passwordGenerator;

        public OrganizationCreatedIntegrationEventHandler(
            ILogger<OrganizationCreatedIntegrationEventHandler> logger,
            ITenantRepository tenantRepository,
            IUserRepository userRepository,
            IPasswordGenerator passwordGenerator)
        {
            _logger = logger;
            _tenantRepository = tenantRepository;
            _userRepository = userRepository;
            _passwordGenerator = passwordGenerator;
        }

        public async Task Handle(OrganizationCreatedIntegrationEvent @event)
        {
            _logger.LogInformation(
                "Identity - Processing organization created event: {EventId} - {OrganizationId} - {Name}",
                @event.Id,
                @event.OrganizationId,
                @event.Name);

            // Telemetry, idempotency check and transaction management are handled automatically by decorators
            // Just focus on business logic here
            // Just focus on business logic here

            // Create Tenant
            var tenant = await CreateTenantAsync(@event);

            // Create Admin User
            var adminUser = await CreateAdminUserAsync(@event, tenant.Id);

            _logger.LogInformation(
                "Identity - Tenant {TenantId} and Admin User {UserId} created for organization {OrganizationId}",
                tenant.Id, adminUser.Id, @event.OrganizationId);
        }

        private async Task<Tenant> CreateTenantAsync(OrganizationCreatedIntegrationEvent @event)
        {
            var tenant = Tenant.Create(
                 id: @event.TenantId,
                 organizationId: @event.OrganizationId,
                 name: @event.Name
             );

            return await _tenantRepository.CreateAsync(tenant);
        }

        private async Task<ApplicationUser> CreateAdminUserAsync(OrganizationCreatedIntegrationEvent @event, Guid tenantId)
        {

            var adminUser = ApplicationUser.CreateAsUnConfirmed(
                 id: Guid.NewGuid(),
                 userName: $"admin-{tenantId.ToString().Substring(0, 8)}",
                 tenantId: tenantId,
                 email: GenerateAdminEmail(@event.Name)
             );


            return await _userRepository.CreateAsync(adminUser);
        }

        private static string GenerateAdminEmail(string organizationName)
        {
            var sanitizedName = organizationName.ToLower()
                .Replace(" ", "")
                .Replace(".", "")
                .Replace(",", "");

            return $"admin@{sanitizedName}.com";
        }
    }
}
