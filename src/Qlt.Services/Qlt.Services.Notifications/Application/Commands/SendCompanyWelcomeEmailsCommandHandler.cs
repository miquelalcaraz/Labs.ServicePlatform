using Microsoft.Extensions.Logging;

using Qlt.Platform;
using Qlt.Platform.Messaging.Commands;
using Qlt.Services.Notifications.Domain.Models;
using Qlt.Services.Notifications.Domain.Repositories;
using Qlt.Services.Notifications.Domain.Services;

namespace Qlt.Services.Notifications.Application.Commands;

/// <summary>
/// Sends welcome and sales notification emails for a newly created company, persisting each as an EmailNotification.
/// </summary>
public class SendCompanyWelcomeEmailsCommandHandler
    : ICommandHandler<SendCompanyWelcomeEmailsCommand, bool>
{
    private readonly IEmailNotificationRepository _repository;
    private readonly IEmailService _emailService;
    private readonly ILogger<SendCompanyWelcomeEmailsCommandHandler> _logger;

    public SendCompanyWelcomeEmailsCommandHandler(
        IEmailNotificationRepository repository,
        IEmailService emailService,
        ILogger<SendCompanyWelcomeEmailsCommandHandler> logger)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _emailService = emailService ?? throw new ArgumentNullException(nameof(emailService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<Result<bool>> Handle(SendCompanyWelcomeEmailsCommand message, CancellationToken cancellationToken)
    {
        var welcomeRecipient = GeneratePlaceholderEmail(message.TradeName);

        _logger.LogWarning(
            "CompanyCreatedIntegrationEvent does not include admin email. Using generated placeholder: {PlaceholderEmail}",
            welcomeRecipient);

        var welcomeNotification = EmailNotification.Create(
            welcomeRecipient,
            $"Welcome to the platform, {message.TradeName}!",
            $"Dear {message.TradeName} team,\n\nWelcome to our B2B platform!\n\nBest regards,\nThe Team",
            EmailType.WelcomeOrganization);

        _repository.Add(welcomeNotification);
        await _emailService.SendAsync(welcomeNotification.To, welcomeNotification.Subject, welcomeNotification.Body);
        welcomeNotification.MarkSent();

        var salesNotification = EmailNotification.Create(
            "sales@company.com",
            $"New Organization Registered: {message.TradeName}",
            $"New organization registered:\n\nName: {message.TradeName}\nLegal Name: {message.LegalName}\nCountry: {message.CountryCode}",
            EmailType.SalesNotification);

        _repository.Add(salesNotification);
        await _emailService.SendAsync(salesNotification.To, salesNotification.Subject, salesNotification.Body);
        salesNotification.MarkSent();

        await _repository.UnitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Welcome and sales notification emails sent for company {CompanyId}",
            message.CompanyId);

        return Result.Success(true);
    }

    private static string GeneratePlaceholderEmail(string companyName) =>
        $"admin@{companyName.ToLower().Replace(" ", "").Replace(".", "").Replace(",", "")}.com";
}
