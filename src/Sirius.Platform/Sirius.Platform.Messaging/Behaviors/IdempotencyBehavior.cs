using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

using MediatR;

using Microsoft.Extensions.Logging;

using Sirius.Platform.Activities;
using Sirius.Platform.Extensions;
using Sirius.Platform.Messaging.Commands;
using Sirius.Platform.Messaging.Models;
using Sirius.Platform.Messaging.Services;

namespace Sirius.Platform.Messaging.Behaviors;

/// <summary>
/// Pipeline behavior that provides automatic idempotency for commands implementing IIdempotentRequest.
/// Checks if a request has been processed before and returns a cached result for duplicates.
/// When the command returns a failed <see cref="Result"/> or throws, the idempotency key is discarded so the request can be retried.
/// </summary>
public class IdempotencyBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private readonly IIdempotencyService? _idempotencyService;
    private readonly ILogger<IdempotencyBehavior<TRequest, TResponse>> _logger;

    public IdempotencyBehavior(
        ILogger<IdempotencyBehavior<TRequest, TResponse>> logger,
        IIdempotencyService? idempotencyService = null)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _idempotencyService = idempotencyService;
    }

    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        if (_idempotencyService is null || request is not IIdempotentRequest idempotentRequest)
            return await next(cancellationToken);

        var commandName = typeof(TRequest).GetGenericTypeName();
        var requestId = idempotentRequest.RequestId;
        var key = IdempotencyKey.ForRequest(requestId);

        using var activity = IdempotencyHelper.StartCommandIdempotencyCheck(requestId, commandName);

        var stopwatch = Stopwatch.StartNew();
        var alreadyExists = await _idempotencyService.ExistsAsync(key);
        stopwatch.Stop();

        var checkDurationMs = stopwatch.Elapsed.TotalMilliseconds;
        IdempotencyHelper.RecordCheckResult(activity, alreadyExists, checkDurationMs);
        IdempotencyHelper.RecordCheckDurationMetric("command", alreadyExists, checkDurationMs);

        if (alreadyExists)
        {
            IdempotencyHelper.RecordDuplicateCommand(activity, commandName, requestId);

            _logger.LogInformation(
                "Duplicate request detected for {CommandName} with RequestId {RequestId}. Returning cached result.",
                commandName,
                requestId);

            return (TResponse)idempotentRequest.CreateDuplicateResult();
        }

        await _idempotencyService.MarkAsProcessingAsync(key, typeof(TRequest).Name);

        try
        {
            var response = await next(cancellationToken);

            if (response is Result { IsFailure: true } failedResult)
            {
                await _idempotencyService.DiscardAsync(key);

                _logger.LogInformation(
                    "Discarded idempotency entry for {CommandName} with RequestId {RequestId} because the command failed ({ErrorCode}). The request can be safely retried.",
                    commandName,
                    requestId,
                    failedResult.Error.Code);

                return response;
            }

            await _idempotencyService.MarkAsCompletedAsync(key);

            return response;
        }
        catch (Exception ex)
        {
            await TryDiscardAsync(key, commandName, requestId);

            _logger.LogError(ex,
                "Failed to process idempotent command {CommandName} with RequestId {RequestId}",
                commandName,
                requestId);
            throw;
        }
    }

    /// <summary>
    /// Discards the idempotency key after an exception without masking the original exception.
    /// </summary>
    private async Task TryDiscardAsync(IdempotencyKey key, string commandName, Guid requestId)
    {
        try
        {
            await _idempotencyService!.DiscardAsync(key);
        }
        catch (Exception discardException)
        {
            _logger.LogWarning(discardException,
                "Failed to discard idempotency entry for {CommandName} with RequestId {RequestId} after exception",
                commandName,
                requestId);
        }
    }
}
