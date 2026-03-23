using QltS.Accounts.Contracts.IntegrationEvents.Events;
using QltS.Identity.Infrastructure;
using QltS.Identity.Models;
using QltS.Identity.Repositories;
using QltS.Identity.Services;
using QltS.Shared.Messaging.Inbox;
using QltS.Shared.Messaging.IntegrationEvents;
using QltS.Shared.Persistence.EF.Relational;

namespace QltS.Identity.IntegrationEvents.EventHandling
{
    public class OrganizationCreatedIntegrationEventHandler : IntegrationEventHandlerBase<OrganizationCreatedIntegrationEvent>
    {
        private readonly ILogger<OrganizationCreatedIntegrationEventHandler> _logger;
        private readonly ITenantRepository _tenantRepository;
        private readonly IUserRepository _userRepository;
        private readonly IPasswordGenerator _passwordGenerator;
        private readonly IIntegrationEventInbox _idempotencyService;
        private readonly IdentityContext _context;

        public OrganizationCreatedIntegrationEventHandler(
            ILogger<OrganizationCreatedIntegrationEventHandler> logger,
            ITenantRepository tenantRepository,
            IUserRepository userRepository,
            IPasswordGenerator passwordGenerator,
            IIntegrationEventInbox idempotencyService,
                IdentityContext context)
        {
            _logger = logger;
            _tenantRepository = tenantRepository;
            _userRepository = userRepository;
            _passwordGenerator = passwordGenerator;
            _idempotencyService = idempotencyService;
            _context = context;
        }

        protected override async Task HandleAsync(OrganizationCreatedIntegrationEvent @event)
        {
            var handlerName = GetType().Name;

            _logger.LogInformation(
                "Identity - Processing organization created event: {EventId} - {OrganizationId} - {Name}",
                @event.Id,
                @event.OrganizationId,
                @event.Name);

            // Check if event already processed
            if (await _idempotencyService.IsProcessedAsync(@event.Id, handlerName))
            {
                _logger.LogWarning(
                    "Identity - Event {EventId} already processed. Skipping duplicate.",
                    @event.Id);
                return;
            }
            // Execute all logic within a resilient transaction
            await ResilientTransaction.New(_context).ExecuteAsync(async () =>
            {
                // Create Tenant
                var tenant = await CreateTenantAsync(@event);

                // Create Admin User
                var adminUser = await CreateAdminUserAsync(@event, tenant.Id);

                // Mark as processed
                await _idempotencyService.MarkAsProcessedAsync(
                    @event.Id,
                    handlerName,
                    @event.GetType().Name);

                _logger.LogInformation(
                    "Identity - Tenant {TenantId} and Admin User {UserId} created for organization {OrganizationId}",
                    tenant.Id, adminUser.Id, @event.OrganizationId);
            });

            _logger.LogInformation(
                "Identity - Successfully completed resilient transaction for event {EventId}",
                @event.Id);
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
