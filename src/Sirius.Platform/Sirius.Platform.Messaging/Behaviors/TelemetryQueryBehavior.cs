using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using MediatR;

using Sirius.Platform.Activities;
using Sirius.Platform.Messaging.Queries;

namespace Sirius.Platform.Messaging.Behaviors;

/// <summary>
/// Pipeline behavior that adds telemetry (OpenTelemetry activities and metrics) to query handlers.
/// Automatically wraps all IQuery executions with distributed tracing and performance metrics.
/// Only activates for requests implementing IQuery&lt;TResponse&gt;.
/// </summary>
public class TelemetryQueryBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private static readonly bool IsQuery = DetectQueryType();

    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        if (!IsQuery)
            return await next(cancellationToken);

        var queryType = typeof(TRequest).Name;

        using var activity = ActivityHelper.StartHandlerActivity(
            activityName: $"Execute {queryType}");

        ActivityHelper.SetHandlerTags(activity, queryType, "pipeline", "query");

        var stopwatch = Stopwatch.StartNew();

        try
        {
            var response = await next(cancellationToken);
            stopwatch.Stop();

            var elapsedMs = stopwatch.Elapsed.TotalMilliseconds;
            var success = response is Result result ? result.IsSuccess : true;

            MetricsHelper.RecordQueryHandlerExecution(queryType, "pipeline", elapsedMs, success);
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

            MetricsHelper.RecordQueryHandlerExecution(queryType, "pipeline", elapsedMs, success: false);
            ActivityHelper.RecordHandlerSuccess(activity, elapsedMs, success: false);
            ActivityHelper.RecordException(activity, ex);

            throw;
        }
    }

    private static bool DetectQueryType()
    {
        return typeof(TRequest).GetInterfaces()
            .Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IQuery<>));
    }
}
