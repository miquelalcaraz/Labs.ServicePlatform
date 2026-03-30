using Qlt.Platform.Messaging.IntegrationEvents;
using Qlt.Services.Accounts.Contracts.IntegrationEvents;
using Qlt.Services.Notifications.Repositories;
using Qlt.Services.Notifications.Services;

namespace Qlt.Services.Notifications.IntegrationEvents.EventHandling
{
    /// <summary>
    /// Handles organization created integration events.
    /// Telemetry and idempotency are automatically enforced by decorators.
    /// </summary>
    public class OrganizationCreatedIntegrationEventHandler : IIntegrationEventHandler<OrganizationCreatedIntegrationEvent>
    {
        private readonly ILogger<OrganizationCreatedIntegrationEventHandler> _logger;
        private readonly IEmailService _emailService;
        private readonly IEmailRepository _userRepository;

        public OrganizationCreatedIntegrationEventHandler(
            ILogger<OrganizationCreatedIntegrationEventHandler> logger,
            IEmailService emailService,
            IEmailRepository userRepository)
        {
            _logger = logger;
            _emailService = emailService;
            _userRepository = userRepository;
        }

        public async Task Handle(OrganizationCreatedIntegrationEvent @event)
        {
            _logger.LogInformation(
                "Notifications - Processing organization created event: {EventId} - {OrganizationId} - {Name}",
                @event.Id,
                @event.OrganizationId,
                @event.Name);

            // Telemetry, idempotency check and transaction management are handled automatically by decorators
            // Just focus on business logic here
            await SendWelcomeEmailAsync(@event);
            await NotifySalesTeamAsync(@event);

            //TODO: Add email record to database for tracking

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
