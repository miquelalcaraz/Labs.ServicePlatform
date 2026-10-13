using Miqa.Platform.Messaging.Commands;

namespace Miqa.Services.Notifications.Application.Commands;

public record SendCompanyWelcomeEmailsCommand(
    Guid CompanyId,
    string TradeName,
    string LegalName,
    string CountryCode) : ICommand<bool>;
