using Sirius.Platform.Messaging.Commands;

namespace Sirius.Services.Accounts.Application.Commands;

public record ConfirmActivationDeliveryCommand(Guid ActivationProcessId) : ICommand<bool>;
