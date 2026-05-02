using Qlt.Platform.Messaging.Commands;

namespace Qlt.Services.Accounts.Application.Commands;

public record CompleteActivationOnUserActivatedCommand(Guid UserId) : ICommand<bool>;
