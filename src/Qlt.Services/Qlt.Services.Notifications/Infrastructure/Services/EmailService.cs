using Microsoft.Extensions.Logging;

using Qlt.Services.Notifications.Domain.Services;

namespace Qlt.Services.Notifications.Infrastructure.Services;

public class EmailService : IEmailService
{
    private readonly ILogger<EmailService> _logger;

    public EmailService(ILogger<EmailService> logger)
    {
        _logger = logger;
    }

    public Task SendAsync(string to, string subject, string body)
    {
        _logger.LogInformation(
            "SIMULATED - Email sent to {Email} with subject '{Subject}'",
            to, subject);

        return Task.CompletedTask;
    }
}
