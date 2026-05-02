using Qlt.Platform.Messaging.Commands;

namespace Qlt.Services.Notifications.Application.Commands;

public record SendActivationEmailCommand(
    Guid ActivationProcessId,
    Guid AdminUserId,
    string AdminEmail,
    string? CompanyLegalName,
    Guid? CorrelationId) : ICommand<bool>;
