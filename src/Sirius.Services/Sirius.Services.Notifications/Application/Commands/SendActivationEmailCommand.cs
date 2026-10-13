using Sirius.Platform.Messaging.Commands;

namespace Sirius.Services.Notifications.Application.Commands;

public record SendActivationEmailCommand(
    Guid ActivationProcessId,
    Guid AdminUserId,
    string AdminEmail,
    string? CompanyLegalName,
    Guid? CorrelationId) : ICommand<bool>;
