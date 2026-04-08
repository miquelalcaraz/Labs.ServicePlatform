using MediatR;

using Qlt.Platform.Activities;
using Qlt.Platform.Messaging.Commands;

using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Qlt.Platform.Messaging.Behaviors;

/// <summary>
/// Pipeline behavior that adds telemetry (OpenTelemetry activities and metrics) to command handlers.
/// Automatically wraps all ICommand executions with distributed tracing and performance metrics.
/// Only activates for requests implementing ICommand or ICommand&lt;TResponse&gt;.
/// </summary>
/// <typeparam name="TRequest">The request type</typeparam>
/// <typeparam name="TResponse">The response type</typeparam>
public class TelemetryCommandBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private static readonly bool IsCommand = DetectCommandType();

    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        if (!IsCommand)
            return await next(cancellationToken);

        var commandType = typeof(TRequest).Name;

        using var activity = ActivityHelper.StartHandlerActivity(
            activityName: $"Execute {commandType}");

        ActivityHelper.SetHandlerTags(activity, commandType, "pipeline", "command");

        var stopwatch = Stopwatch.StartNew();

        try
        {
            var response = await next(cancellationToken);
            stopwatch.Stop();

            var elapsedMs = stopwatch.Elapsed.TotalMilliseconds;
            var success = response is Result result ? result.IsSuccess : true;

            MetricsHelper.RecordCommandHandlerExecution(commandType, "pipeline", elapsedMs, success);
            ActivityHelper.RecordHandlerSuccess(activity, elapsedMs, success);

            if (response is Result failedResult && !failedResult.IsSuccess && failedResult.Error != null)
            {
                ActivityHelper.RecordResultError(activity, failedResult.Error.Code, failedResult.Error.Message);
            }

            return response;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            var elapsedMs = stopwatch.Elapsed.TotalMilliseconds;

            MetricsHelper.RecordCommandHandlerExecution(commandType, "pipeline", elapsedMs, success: false);
            ActivityHelper.RecordHandlerSuccess(activity, elapsedMs, success: false);
            ActivityHelper.RecordException(activity, ex);

            throw;
        }
    }

    private static bool DetectCommandType()
    {
        var requestType = typeof(TRequest);

        if (typeof(ICommand).IsAssignableFrom(requestType))
            return true;

        return requestType.GetInterfaces()
            .Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(ICommand<>));
    }
}
