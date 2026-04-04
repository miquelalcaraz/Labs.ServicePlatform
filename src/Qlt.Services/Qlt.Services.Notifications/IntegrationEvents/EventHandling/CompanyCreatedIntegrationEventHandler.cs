using Qlt.Platform.Messaging.IntegrationEvents;
using Qlt.Services.Accounts.Contracts.IntegrationEvents;
using Qlt.Services.Notifications.Repositories;
using Qlt.Services.Notifications.Services;

namespace Qlt.Services.Notifications.IntegrationEvents.EventHandling;

/// <summary>
/// Handles company created integration events from Account service.
/// Telemetry and idempotency are automatically enforced by decorators.
/// </summary>
public class CompanyCreatedIntegrationEventHandler : IIntegrationEventHandler<CompanyCreatedIntegrationEvent>
{
    private readonly ILogger<CompanyCreatedIntegrationEventHandler> _logger;
    private readonly IEmailService _emailService;
    private readonly IEmailRepository _userRepository;

    public CompanyCreatedIntegrationEventHandler(
        ILogger<CompanyCreatedIntegrationEventHandler> logger,
        IEmailService emailService,
        IEmailRepository userRepository)
    {
        _logger = logger;
        _emailService = emailService;
        _userRepository = userRepository;
    }

    public async Task Handle(CompanyCreatedIntegrationEvent @event)
    {
        _logger.LogInformation(
            "Notifications - Processing company created event: {EventId} - {CompanyId} - {LegalName}",
            @event.Id,
            @event.CompanyId,
            @event.LegalName);

        await SendWelcomeEmailAsync(@event);
        await NotifySalesTeamAsync(@event);

        _logger.LogInformation(
            "Notifications - Emails sent for company {CompanyId}",
            @event.CompanyId);
    }

    private async Task SendWelcomeEmailAsync(CompanyCreatedIntegrationEvent @event)
    {
        var adminEmail = GenerateAdminEmail(@event.TradeName);
        await _emailService.SendWelcomeEmailAsync(adminEmail, @event.TradeName);
    }

    private async Task NotifySalesTeamAsync(CompanyCreatedIntegrationEvent @event)
    {
        await _emailService.SendSalesNotificationAsync(
            @event.TradeName,
            @event.LegalName,
            @event.CountryCode);
    }

    private static string GenerateAdminEmail(string companyName)
    {
        var sanitizedName = companyName.ToLower()
            .Replace(" ", "")
            .Replace(".", "")
            .Replace(",", "");

        return $"admin@{sanitizedName}.com";
    }
}
