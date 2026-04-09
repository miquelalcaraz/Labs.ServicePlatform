using MediatR;

using Microsoft.Extensions.Logging;

using Qlt.Platform.Activities;
using Qlt.Platform.Extensions;
using Qlt.Platform.Messaging.Commands;
using Qlt.Platform.Messaging.Models;
using Qlt.Platform.Messaging.Services;

using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace Qlt.Platform.Messaging.Behaviors;

/// <summary>
/// Pipeline behavior that provides automatic idempotency for commands implementing IIdempotentRequest.
/// Checks if a request has been processed before and returns a cached result for duplicates.
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

            await _idempotencyService.MarkAsCompletedAsync(key);

            return response;
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
}
