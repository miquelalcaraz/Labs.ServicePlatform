namespace Qlt.Services.Notifications.Services
{
    public interface IEmailService
    {
        Task SendWelcomeEmailAsync(string toEmail, string organizationName);
        Task SendSalesNotificationAsync(string organizationName, string legalName, string countryCode);
    }
}
