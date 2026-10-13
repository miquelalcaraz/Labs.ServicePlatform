using System.Runtime.Serialization;

using Miqa.Platform;
using Miqa.Platform.Messaging.Commands;

namespace Miqa.Services.Accounts.Application.Commands;

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
