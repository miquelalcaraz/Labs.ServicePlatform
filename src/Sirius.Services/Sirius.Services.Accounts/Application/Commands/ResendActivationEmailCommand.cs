using System.Runtime.Serialization;

using Sirius.Platform;
using Sirius.Platform.Messaging.Commands;

namespace Sirius.Services.Accounts.Application.Commands;

[DataContract]
public record ResendActivationEmailCommand : IdempotentCommand<bool>
{
    [DataMember] public Guid ActivationProcessId { get; init; }

    public ResendActivationEmailCommand() { }

    public ResendActivationEmailCommand(Guid activationProcessId, Guid? requestId = null) : base(requestId)
    {
        ActivationProcessId = activationProcessId;
    }

    public override Result<bool> CreateResultForDuplicateRequest() => Result.Success(true);
}
