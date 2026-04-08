using Qlt.Platform;
using Qlt.Platform.Messaging.Commands;

using System.Runtime.Serialization;

namespace Qlt.Services.Accounts.Application.Commands;

[DataContract]
public record RetryOnboardingProcessCommand : IdempotentCommand<bool>
{
    [DataMember]
    public Guid OnboardingProcessId { get; private set; }

    [DataMember]
    public string? Reason { get; private set; }

    public RetryOnboardingProcessCommand() { }

    public RetryOnboardingProcessCommand(Guid? requestId = null) : base(requestId) { }

    public RetryOnboardingProcessCommand(
        Guid onboardingProcessId,
        string? reason = null,
        Guid? requestId = null) : base(requestId)
    {
        OnboardingProcessId = onboardingProcessId;
        Reason = reason;
    }

    public override Result<bool> CreateResultForDuplicateRequest()
    {
        return Result.Success(true);
    }
}
