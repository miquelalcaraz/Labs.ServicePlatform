using MediatR;

using Microsoft.Extensions.Logging;

using QltS.Shared.Extensions;
using QltS.Shared.Messaging.Idempotency;
using QltS.Shared.Telemetry.Activities;

using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace QltS.Shared.Messaging.Behaviors
{
    /// <summary>
    /// Pipeline behavior that provides automatic idempotency for commands implementing IIdempotentCommand.
    /// Checks if a request has been processed before and returns cached result for duplicates.
    /// Integrates seamlessly with CommandHandlerBase and other pipeline behaviors.
    /// </summary>
    /// <typeparam name="TRequest">The request type (command)</typeparam>
    /// <typeparam name="TResponse">The response type (Result or Result&lt;T&gt;)</typeparam>
    public class IdempotencyBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
        where TRequest : IRequest<TResponse>
    {
        private readonly IRequestIdempotencyService _requestManager;
        private readonly ILogger<IdempotencyBehavior<TRequest, TResponse>> _logger;

        public IdempotencyBehavior(
            IRequestIdempotencyService requestManager,
            ILogger<IdempotencyBehavior<TRequest, TResponse>> logger)
        {
            _requestManager = requestManager ?? throw new ArgumentNullException(nameof(requestManager));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
        {
            // Check if this is an idempotent command by checking if it has RequestId property
            // We can't use IIdempotentCommand<TResponse> directly because TResponse is Result<T>, 
            // but the interface is implemented with just T
            var idempotentCommandType = request.GetType().GetInterfaces()
                .FirstOrDefault(i => i.IsGenericType &&
                                    (i.GetGenericTypeDefinition() == typeof(IIdempotentCommand<>) ||
                                     i == typeof(IIdempotentCommand)));

            if (idempotentCommandType == null)
            {
                // Not an idempotent command, continue pipeline
                return await next();
            }

            var requestId = GetRequestId(request);
            var commandName = typeof(TRequest).GetGenericTypeName();

            // Start activity for idempotency check
            using var activity = IdempotencyHelper.StartCommandIdempotencyCheck(requestId, commandName);

            var stopwatch = Stopwatch.StartNew();
            var alreadyExists = await _requestManager.ExistAsync(requestId);
            stopwatch.Stop();

            var checkDurationMs = stopwatch.Elapsed.TotalMilliseconds;

            // Record idempotency check result
            IdempotencyHelper.RecordCheckResult(activity, alreadyExists, checkDurationMs);
            IdempotencyHelper.RecordCheckDurationMetric("command", alreadyExists, checkDurationMs);

            if (alreadyExists)
            {
                // Record duplicate command detection
                IdempotencyHelper.RecordDuplicateCommand(activity, commandName, requestId);

                _logger.LogInformation(
                    "Duplicate request detected for {CommandName} with RequestId {RequestId}. Returning cached result.",
                    commandName,
                    requestId);

                // Return the duplicate result based on command type
                return GetDuplicateResult(request);
            }

            // Mark request as being processed (before executing to prevent race conditions)
            await _requestManager.CreateRequestForCommandAsync<TRequest>(requestId);

            try
            {
                _logger.LogInformation(
                    "Processing idempotent command {CommandName} with RequestId {RequestId}",
                    commandName,
                    requestId);

                // Continue with the pipeline (validation, transaction, handler)
                var result = await next();

                _logger.LogInformation(
                    "Successfully processed idempotent command {CommandName} with RequestId {RequestId}",
                    commandName,
                    requestId);

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Failed to process idempotent command {CommandName} with RequestId {RequestId}",
                    commandName,
                    requestId);
                throw;
            }
        }

        private static Guid GetRequestId(TRequest request)
        {
            // Try to get RequestId from any IIdempotentCommand or IIdempotentCommand<T> implementation
            var interfaces = request.GetType().GetInterfaces();

            // Check for IIdempotentCommand (non-generic)
            if (interfaces.Any(i => i == typeof(IIdempotentCommand)))
            {
                return ((IIdempotentCommand)request).RequestId;
            }

            // Check for IIdempotentCommand<T> (generic, regardless of T)
            var genericInterface = interfaces.FirstOrDefault(i => 
                i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IIdempotentCommand<>));

            if (genericInterface != null)
            {
                var requestIdProperty = genericInterface.GetProperty(nameof(IIdempotentCommand.RequestId));
                return (Guid)requestIdProperty!.GetValue(request)!;
            }

            throw new InvalidOperationException("Request does not implement IIdempotentCommand");
        }

        private static TResponse GetDuplicateResult(TRequest request)
        {
            // Try to get duplicate result from any IIdempotentCommand or IIdempotentCommand<T> implementation
            var interfaces = request.GetType().GetInterfaces();

            // Check for IIdempotentCommand (non-generic)
            if (interfaces.Any(i => i == typeof(IIdempotentCommand)))
            {
                var result = ((IIdempotentCommand)request).CreateResultForDuplicateRequest();
                return (TResponse)(object)result;
            }

            // Check for IIdempotentCommand<T> (generic, regardless of T)
            var genericInterface = interfaces.FirstOrDefault(i => 
                i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IIdempotentCommand<>));

            if (genericInterface != null)
            {
                var method = genericInterface.GetMethod(nameof(IIdempotentCommand.CreateResultForDuplicateRequest));
                var result = method!.Invoke(request, null);
                return (TResponse)(object)result!;
            }

            throw new InvalidOperationException("Request does not implement IIdempotentCommand");
        }
    }

}
