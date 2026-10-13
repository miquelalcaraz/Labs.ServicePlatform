using Sirius.Platform.Messaging.Commands;

namespace Sirius.Services.Accounts.Application.Commands;

public record CompleteActivationOnUserActivatedCommand(Guid UserId) : ICommand<bool>;
