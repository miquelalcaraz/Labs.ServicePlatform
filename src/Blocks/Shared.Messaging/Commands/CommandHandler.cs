using QltS.Shared.Telemetry.Activities;

using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace QltS.Shared.Messaging.Commands;

/// <summary>
/// Base class for command handlers with built-in telemetry.
/// Automatically creates Activity spans and records metrics for each handler execution.
/// Complements LoggingBehavior by providing handler-specific telemetry.
/// </summary>
/// <typeparam name="TCommand">The command type</typeparam>
/// <typeparam name="TResponse">The response type wrapped in Result</typeparam>
public abstract class CommandHandler<TCommand, TResponse> : ICommandHandler<TCommand, TResponse>
    where TCommand : ICommand<TResponse>
{
    /// <summary>
    /// Handles the command with automatic telemetry.
    /// Do not override this method - override HandleAsync instead.
    /// </summary>
    public async Task<Result<TResponse>> Handle(TCommand command, CancellationToken cancellationToken)
    {
        var commandType = typeof(TCommand).Name;
        var handlerType = GetType().Name;

        // Start activity for distributed tracing
        using var activity = ActivityHelper.StartHandlerActivity(
            activityName: $"Execute {commandType}");

        ActivityHelper.SetHandlerTags(activity, commandType, handlerType, "command");

        var stopwatch = Stopwatch.StartNew();

        try
        {
            var result = await HandleAsync(command, cancellationToken);
            stopwatch.Stop();

            var elapsedMs = stopwatch.Elapsed.TotalMilliseconds;
            var success = result.IsSuccess;

            // Record handler-specific metrics
            MetricsHelper.RecordCommandHandlerExecution(commandType, handlerType, elapsedMs, success);

            ActivityHelper.RecordHandlerSuccess(activity, elapsedMs, success);

            if (!success && result.Error != null)
            {
                ActivityHelper.RecordResultError(activity, result.Error.Code, result.Error.Message);
            }

            return result;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            var elapsedMs = stopwatch.Elapsed.TotalMilliseconds;

            // Record failed handler execution metric
            MetricsHelper.RecordCommandHandlerExecution(commandType, handlerType, elapsedMs, success: false);

            ActivityHelper.RecordHandlerSuccess(activity, elapsedMs, success: false);
            ActivityHelper.RecordException(activity, ex);

            throw;
        }
    }

    /// <summary>
    /// Override this method to implement your command handling logic.
    /// Telemetry (activities and metrics) will be automatically recorded by the base class.
    /// </summary>
    /// <param name="command">The command to handle</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Result containing the response or error</returns>
    protected abstract Task<Result<TResponse>> HandleAsync(TCommand command, CancellationToken cancellationToken);
}

/// <summary>
/// Base class for command handlers that return Result without a response value.
/// Automatically creates Activity spans and records metrics for each handler execution.
/// </summary>
/// <typeparam name="TCommand">The command type</typeparam>
public abstract class DomainCommandHandler<TCommand> : ICommandHandler<TCommand>
    where TCommand : ICommand
{
    /// <summary>
    /// Handles the command with automatic telemetry.
    /// Do not override this method - override HandleAsync instead.
    /// </summary>
    public async Task<Result> Handle(TCommand command, CancellationToken cancellationToken)
    {
        var commandType = typeof(TCommand).Name;
        var handlerType = GetType().Name;

        // Start activity for distributed tracing
        using var activity = ActivityHelper.StartHandlerActivity(
            activityName: $"Execute {commandType}");

        ActivityHelper.SetHandlerTags(activity, commandType, handlerType, "command");

        var stopwatch = Stopwatch.StartNew();

        try
        {
            var result = await HandleAsync(command, cancellationToken);
            stopwatch.Stop();

            var elapsedMs = stopwatch.Elapsed.TotalMilliseconds;
            var success = result.IsSuccess;

            // Record handler-specific metrics
            MetricsHelper.RecordCommandHandlerExecution(commandType, handlerType, elapsedMs, success);

            ActivityHelper.RecordHandlerSuccess(activity, elapsedMs, success);

            if (!success && result.Error != null)
            {
                ActivityHelper.RecordResultError(activity, result.Error.Code, result.Error.Message);
            }

            return result;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            var elapsedMs = stopwatch.Elapsed.TotalMilliseconds;

            // Record failed handler execution metric
            MetricsHelper.RecordCommandHandlerExecution(commandType, handlerType, elapsedMs, success: false);

            ActivityHelper.RecordHandlerSuccess(activity, elapsedMs, success: false);
            ActivityHelper.RecordException(activity, ex);

            throw;
        }
    }

    /// <summary>
    /// Override this method to implement your command handling logic.
    /// Telemetry (activities and metrics) will be automatically recorded by the base class.
    /// </summary>
    /// <param name="command">The command to handle</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Result indicating success or failure</returns>
    protected abstract Task<Result> HandleAsync(TCommand command, CancellationToken cancellationToken);
}
