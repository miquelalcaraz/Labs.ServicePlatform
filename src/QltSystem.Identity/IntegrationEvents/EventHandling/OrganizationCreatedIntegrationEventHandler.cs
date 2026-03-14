using QltSystem.Accounts.Contracts.IntegrationEvents.Events;
using QltSystem.Core.EventBus.Abstractions;
using QltSystem.Core.EventBus.Services;
using QltSystem.Core.EventBus.TransactionalOutbox.Utilities;
using QltSystem.Identity.Infrastructure;
using QltSystem.Identity.Models;
using QltSystem.Identity.Repositories;
using QltSystem.Identity.Services;

namespace QltSystem.Identity.IntegrationEvents.EventHandling
{
    public class OrganizationCreatedIntegrationEventHandler : IIntegrationEventHandler<OrganizationCreatedIntegrationEvent>
    {
        private readonly ILogger<OrganizationCreatedIntegrationEventHandler> _logger;
        private readonly ITenantRepository _tenantRepository;
        private readonly IUserRepository _userRepository;
        private readonly IPasswordGenerator _passwordGenerator;
        private readonly IEventIdempotencyService _idempotencyService;
        private readonly IdentityContext _context;

        public OrganizationCreatedIntegrationEventHandler(
            ILogger<OrganizationCreatedIntegrationEventHandler> logger,
            ITenantRepository tenantRepository,
            IUserRepository userRepository,
            IPasswordGenerator passwordGenerator,
            IEventIdempotencyService idempotencyService,
                IdentityContext context)
        {
            _logger = logger;
            _tenantRepository = tenantRepository;
            _userRepository = userRepository;
            _passwordGenerator = passwordGenerator;
            _idempotencyService = idempotencyService;
            _context = context;
        }

        public async Task Handle(OrganizationCreatedIntegrationEvent @event)
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
            var tenant = new Tenant
            {
                Id = @event.TenantId,
                OrganizationId = @event.OrganizationId,
                Name = @event.Name,
                CreatedAt = DateTime.UtcNow
            };

            return await _tenantRepository.CreateAsync(tenant);
        }

        private async Task<AdminUser> CreateAdminUserAsync(OrganizationCreatedIntegrationEvent @event, Guid tenantId)
        {
            var adminUser = new AdminUser
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                Email = GenerateAdminEmail(@event.Name),
                TemporaryPassword = _passwordGenerator.GenerateTemporaryPassword(),
                CreatedAt = DateTime.UtcNow
            };

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
