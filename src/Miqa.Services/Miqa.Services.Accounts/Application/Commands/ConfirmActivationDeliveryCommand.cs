using Miqa.Platform.Messaging.Commands;

namespace Miqa.Services.Accounts.Application.Commands;

public record ConfirmActivationDeliveryCommand(Guid ActivationProcessId) : ICommand<bool>;
