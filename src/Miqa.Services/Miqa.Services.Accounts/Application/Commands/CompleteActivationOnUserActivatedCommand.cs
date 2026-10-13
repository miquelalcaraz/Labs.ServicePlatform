using Miqa.Platform.Messaging.Commands;

namespace Miqa.Services.Accounts.Application.Commands;

public record CompleteActivationOnUserActivatedCommand(Guid UserId) : ICommand<bool>;
