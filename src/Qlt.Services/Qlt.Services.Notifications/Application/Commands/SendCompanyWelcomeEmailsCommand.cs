using Qlt.Platform.Messaging.Commands;

namespace Qlt.Services.Notifications.Application.Commands;

public record SendCompanyWelcomeEmailsCommand(
    Guid CompanyId,
    string TradeName,
    string LegalName,
    string CountryCode) : ICommand<bool>;
