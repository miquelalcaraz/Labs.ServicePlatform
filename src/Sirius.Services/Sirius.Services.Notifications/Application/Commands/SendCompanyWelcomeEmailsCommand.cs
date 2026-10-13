using Sirius.Platform.Messaging.Commands;

namespace Sirius.Services.Notifications.Application.Commands;

public record SendCompanyWelcomeEmailsCommand(
    Guid CompanyId,
    string TradeName,
    string LegalName,
    string CountryCode) : ICommand<bool>;
