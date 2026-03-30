using Qlt.Platform.Messaging.Commands;

namespace Qlt.Platform.Messaging.Idempotency
{
    /// <summary>
    /// Interface for commands that require idempotency checks with a typed response.
    /// </summary>
    public interface IIdempotentCommand<TResponse> : ICommand<TResponse>, IIdempotentRequest
    {
        Result<TResponse> CreateResultForDuplicateRequest()
        {
            return Result.Success(default(TResponse)!);
        }

        object IIdempotentRequest.CreateDuplicateResult()
            => CreateResultForDuplicateRequest();
    }

    /// <summary>
    /// Interface for commands that require idempotency checks returning Result without a value.
    /// </summary>
    public interface IIdempotentCommand : ICommand, IIdempotentRequest
    {
        Result CreateResultForDuplicateRequest()
        {
            return Result.Success();
        }

        object IIdempotentRequest.CreateDuplicateResult()
            => CreateResultForDuplicateRequest();
    }
}
