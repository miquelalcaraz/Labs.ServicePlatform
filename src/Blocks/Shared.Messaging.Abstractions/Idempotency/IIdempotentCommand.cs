using QltS.Shared.Messaging.Commands;

namespace QltS.Shared.Messaging.Idempotency
{
    /// <summary>
    /// Marker interface for commands that require idempotency checks.
    /// Commands implementing this interface will have automatic duplicate detection
    /// based on a unique request identifier.
    /// </summary>
    /// <typeparam name="TResponse">The response type wrapped in Result</typeparam>
    public interface IIdempotentCommand<TResponse> : ICommand<TResponse>
    {
        /// <summary>
        /// Unique identifier for this command execution.
        /// Typically comes from x-requestid header or event id.
        /// Used to detect and prevent duplicate processing.
        /// </summary>
        System.Guid RequestId { get; }

        /// <summary>
        /// Creates the result to return when a duplicate request is detected.
        /// Override this to specify custom behavior for duplicates.
        /// By default, returns a successful result.
        /// </summary>
        /// <returns>Result to return for duplicate requests</returns>
        Result<TResponse> CreateResultForDuplicateRequest()
        {
            return Result.Success(default(TResponse)!);
        }
    }

    /// <summary>
    /// Marker interface for commands that require idempotency checks and return Result without a value.
    /// </summary>
    public interface IIdempotentCommand : ICommand
    {
        /// <summary>
        /// Unique identifier for this command execution.
        /// Typically comes from x-requestid header or event id.
        /// Used to detect and prevent duplicate processing.
        /// </summary>
        System.Guid RequestId { get; }

        /// <summary>
        /// Creates the result to return when a duplicate request is detected.
        /// Override this to specify custom behavior for duplicates.
        /// By default, returns a successful result.
        /// </summary>
        /// <returns>Result to return for duplicate requests</returns>
        Result CreateResultForDuplicateRequest()
        {
            return Result.Success();
        }
    }

}
