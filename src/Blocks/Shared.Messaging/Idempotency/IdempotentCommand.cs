using System;

namespace QltS.Shared.Messaging.Idempotency
{
    /// <summary>
    /// Base record for idempotent commands that return a result value.
    /// Provides automatic RequestId generation and default duplicate handling.
    /// Use this for convenience instead of implementing IIdempotentCommand directly.
    /// </summary>
    /// <typeparam name="TResponse">The response type wrapped in Result</typeparam>
    public abstract record IdempotentCommand<TResponse> : IIdempotentCommand<TResponse>
    {

        protected IdempotentCommand(Guid? requestId = null)
        {
            RequestId = requestId ?? Guid.NewGuid();
        }

        /// <summary>
        /// Unique identifier for this command execution.
        /// Automatically initialized with a new Guid, but can be set via init.
        /// </summary>
        public System.Guid RequestId { get; init; } = System.Guid.NewGuid();

        /// <summary>
        /// Creates the result to return when a duplicate request is detected.
        /// Override this to specify custom behavior for duplicates.
        /// By default, returns a successful result with default value.
        /// </summary>
        /// <returns>Result to return for duplicate requests</returns>
        public virtual Result<TResponse> CreateResultForDuplicateRequest()
        {
            return Result.Success(default(TResponse)!);
        }
    }

    /// <summary>
    /// Base record for idempotent commands that return Result without a value.
    /// Provides automatic RequestId generation and default duplicate handling.
    /// Use this for convenience instead of implementing IIdempotentCommand directly.
    /// </summary>
    public abstract record IdempotentCommand : IIdempotentCommand
    {
        protected IdempotentCommand(Guid? requestId = null)
        {
            RequestId = requestId ?? Guid.NewGuid();
        }

        /// <summary>
        /// Unique identifier for this command execution.
        /// Automatically initialized with a new Guid, but can be set via init.
        /// </summary>
        public System.Guid RequestId { get; init; } = System.Guid.NewGuid();

        /// <summary>
        /// Creates the result to return when a duplicate request is detected.
        /// Override this to specify custom behavior for duplicates.
        /// By default, returns a successful result.
        /// </summary>
        /// <returns>Result to return for duplicate requests</returns>
        public virtual Result CreateResultForDuplicateRequest()
        {
            return Result.Success();
        }
    }

}
