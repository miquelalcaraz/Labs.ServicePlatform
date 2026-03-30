using Qlt.Services.Notifications.Models;

using System.Collections.Concurrent;

namespace Qlt.Services.Notifications.Services
{
    public class EmailService : IEmailService
    {
        private readonly ILogger<EmailService> _logger;
        private readonly ConcurrentBag<EmailNotification> _sentEmails = new();

        public EmailService(ILogger<EmailService> logger)
        {
            _logger = logger;
        }

        public Task SendWelcomeEmailAsync(string toEmail, string organizationName)
        {
            var email = new EmailNotification
            {
                Id = Guid.NewGuid(),
                To = toEmail,
                Subject = $"Welcome to the platform, {organizationName}!",
                Body = $"Dear {organizationName} team,\n\nWelcome to our B2B platform!\n\nBest regards,\nThe Team",
                Type = EmailType.WelcomeOrganization,
                Status = NotificationStatus.Sent,
                CreatedAt = DateTime.UtcNow,
                SentAt = DateTime.UtcNow
            };

            _sentEmails.Add(email);

            _logger.LogInformation(
                "📧 SIMULATED - Welcome email sent to {Email} for organization {OrganizationName}",
                toEmail, organizationName);

            return Task.CompletedTask;
        }

        public Task SendSalesNotificationAsync(string organizationName, string legalName, string countryCode)
        {
            var salesEmail = "sales@company.com"; // Hardcoded for simulation

            var email = new EmailNotification
            {
                Id = Guid.NewGuid(),
                To = salesEmail,
                Subject = $"New Organization Registered: {organizationName}",
                Body = $"New organization registered:\n\nName: {organizationName}\nLegal Name: {legalName}\nCountry: {countryCode}",
                Type = EmailType.SalesNotification,
                Status = NotificationStatus.Sent,
                CreatedAt = DateTime.UtcNow,
                SentAt = DateTime.UtcNow
            };

            _sentEmails.Add(email);

            _logger.LogInformation(
                "📣 SIMULATED - Sales notification sent for organization {OrganizationName} ({LegalName}) from {CountryCode}",
                organizationName, legalName, countryCode);

            return Task.CompletedTask;
        }

        public IEnumerable<EmailNotification> GetSentEmails() => _sentEmails;
    }
}
