using Qlt.Platform.Messaging.Commands;

namespace Qlt.Services.Accounts.Application.Commands;

public record ConfirmActivationDeliveryCommand(Guid ActivationProcessId) : ICommand<bool>;
