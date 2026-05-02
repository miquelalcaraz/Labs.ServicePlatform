using System.Diagnostics;

using MediatR;

using Microsoft.Extensions.Logging;

using Qlt.Domain.Services;
using Qlt.Platform.Activities;


namespace Qlt.Domain.Behaviors;

/// <summary>
/// MediatR pipeline behavior that logs command/query execution with performance tracking.
/// Works with any request context (HTTP, messaging, background jobs) via IUserHttpContext.
/// </summary>
public class LoggingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private readonly ILogger<LoggingBehavior<TRequest, TResponse>> _logger;
    private readonly IUserHttpContext? _contextAccessor;
    private const int SlowCommandThresholdMs = 500;

    public LoggingBehavior(
        ILogger<LoggingBehavior<TRequest, TResponse>> logger,
        IUserHttpContext? contextAccessor = null)
    {
        _logger = logger;
        _contextAccessor = contextAccessor;
    }

    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        var commandName = GetCommandName(request);
        var requestId = _contextAccessor?.RequestId ?? "N/A";
        var correlationId = _contextAccessor?.CorrelationId ?? "N/A";

        using var activity = BehaviorHelper.StartCommandActivity(commandName);
        BehaviorHelper.SetCommandContextTags(activity, commandName, requestId, correlationId);

        using var scope = _logger.BeginScope(new Dictionary<string, object>
        {
            ["CommandName"] = commandName,
            ["RequestId"] = requestId,
            ["CorrelationId"] = correlationId
        });

        _logger.LogInformation(
            "Handling command {CommandName} with RequestId {RequestId}",
            commandName,
            requestId);

        var stopwatch = Stopwatch.StartNew();
        TResponse response;

        try
        {
            response = await next(cancellationToken);
            stopwatch.Stop();

            var elapsedMs = stopwatch.ElapsedMilliseconds;
            var isSlow = elapsedMs > SlowCommandThresholdMs;

            BehaviorHelper.RecordCommandExecution(commandName, elapsedMs, success: true);
            BehaviorHelper.RecordCommandSuccess(activity, elapsedMs, isSlow);

            if (isSlow)
            {
                _logger.LogWarning(
                    "Command {CommandName} took {ElapsedMilliseconds}ms (threshold: {ThresholdMs}ms) - RequestId: {RequestId}",
                    commandName,
                    elapsedMs,
                    SlowCommandThresholdMs,
                    requestId);
            }
            else
            {
                _logger.LogInformation(
                    "Command {CommandName} handled successfully in {ElapsedMilliseconds}ms - RequestId: {RequestId}",
                    commandName,
                    elapsedMs,
                    requestId);
            }

            return response;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();

            var elapsedMs = stopwatch.ElapsedMilliseconds;

            BehaviorHelper.RecordCommandExecution(commandName, elapsedMs, success: false);
            BehaviorHelper.RecordCommandFailure(activity, elapsedMs, ex);

            _logger.LogError(
                ex,
                "Error handling command {CommandName} after {ElapsedMilliseconds}ms - RequestId: {RequestId}",
                commandName,
                elapsedMs,
                requestId);

            throw;
        }
    }

    private static string GetCommandName(TRequest request)
    {
        var type = request.GetType();

        if (type.IsGenericType)
        {
            var genericArgs = string.Join(",", type.GetGenericArguments().Select(t => t.Name));
            var typeName = type.Name[..type.Name.IndexOf('`')];
            return $"{typeName}<{genericArgs}>";
        }

        return type.Name;
    }
}
