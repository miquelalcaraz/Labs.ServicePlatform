using System.Runtime.Serialization;

using Sirius.Platform;
using Sirius.Platform.Messaging.Commands;

namespace Sirius.Services.Accounts.Application.Commands;

[DataContract]
public record RetryCustomerActivationProcessCommand : IdempotentCommand<bool>
{
    [DataMember] public Guid ActivationProcessId { get; init; }
    [DataMember] public string? Reason { get; init; }

    public RetryCustomerActivationProcessCommand() { }

    public RetryCustomerActivationProcessCommand(
        Guid activationProcessId,
        string? reason = null,
        Guid? requestId = null) : base(requestId)
    {
        ActivationProcessId = activationProcessId;
        Reason = reason;
    }

    public override Result<bool> CreateResultForDuplicateRequest() => Result.Success(true);
}
