namespace Sirius.Services.Notifications.Domain.Services;

/// <summary>
/// Sends an email to the specified recipient. Infrastructure implementation handles the delivery mechanism.
/// </summary>
public interface IEmailService
{
    Task SendAsync(string to, string subject, string body);
}
