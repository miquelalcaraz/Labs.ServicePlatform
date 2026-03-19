using QltS.Accounts.Contracts.IntegrationEvents.Events;
using QltS.Core.EventBus.Abstractions;
using QltS.Core.EventBus.Services;
using QltS.Core.EventBus.TransactionalOutbox.Utilities;
using QltS.Notifications.Infrastructure;
using QltS.Notifications.Repositories;
using QltS.Notifications.Services;

namespace QltS.Notifications.IntegrationEvents.EventHandling
{
    public class OrganizationCreatedIntegrationEventHandler : IIntegrationEventHandler<OrganizationCreatedIntegrationEvent>
    {
        private readonly ILogger<OrganizationCreatedIntegrationEventHandler> _logger;
        private readonly IEmailService _emailService;
        private readonly IEmailRepository _userRepository;
        private readonly IEventIdempotencyService _idempotencyService;
        private readonly NotificationsContext _context;

        public OrganizationCreatedIntegrationEventHandler(
            ILogger<OrganizationCreatedIntegrationEventHandler> logger,
            IEmailService emailService,
            IEmailRepository userRepository,
            IEventIdempotencyService idempotencyService,
                NotificationsContext context)
        {
            _logger = logger;
            _emailService = emailService;
            _userRepository = userRepository;
            _idempotencyService = idempotencyService;
            _context = context;
        }

        public async Task Handle(OrganizationCreatedIntegrationEvent @event)
        {
            var handlerName = GetType().Name;

            _logger.LogInformation(
                "Notifications - Processing organization created event: {EventId} - {OrganizationId} - {Name}",
                @event.Id,
                @event.OrganizationId,
                @event.Name);

            // Check if event already processed
            if (await _idempotencyService.IsProcessedAsync(@event.Id, handlerName))
            {
                _logger.LogWarning(
                    "Notifications - Event {EventId} already processed. Skipping duplicate.",
                    @event.Id);
                return;
            }
            // Execute all logic within a resilient transaction
            await ResilientTransaction.New(_context).ExecuteAsync(async () =>
            {
                await SendWelcomeEmailAsync(@event);
                await NotifySalesTeamAsync(@event);

                //TODO; Add email record to database for tracking

                // Mark as processed
                await _idempotencyService.MarkAsProcessedAsync(
                    @event.Id,
                    handlerName,
                    @event.GetType().Name);

                _logger.LogInformation(
             "Notifications - Emails sent for organization {OrganizationId}",
             @event.OrganizationId);
            });

            _logger.LogInformation(
             "Notifications - Emails sent for organization {OrganizationId}",
             @event.OrganizationId);
        }

        private async Task SendWelcomeEmailAsync(OrganizationCreatedIntegrationEvent @event)
        {
            var adminEmail = GenerateAdminEmail(@event.Name);
            await _emailService.SendWelcomeEmailAsync(adminEmail, @event.Name);
        }

        private async Task NotifySalesTeamAsync(OrganizationCreatedIntegrationEvent @event)
        {
            await _emailService.SendSalesNotificationAsync(
                @event.Name,
                @event.LegalName,
                @event.CountryCode);
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
