using MediatR;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using QltS.Shared.Extensions;
using QltS.Shared.Messaging.Outbox;
using QltS.Shared.Telemetry.Activities;

using System.Diagnostics;



namespace QltS.Shared.Persistence.EF.Behaviors;

/// <summary>
/// Generic transaction behavior that wraps command execution in a database transaction.
/// Implements the Outbox Pattern by publishing integration events only after successful commit.
/// </summary>
/// <typeparam name="TRequest">The command/request type</typeparam>
/// <typeparam name="TResponse">The response type</typeparam>
public class TransactionBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private readonly ILogger<TransactionBehavior<TRequest, TResponse>> _logger;
    private readonly QltDBContextBase _dbContext;
    private readonly IEventBusOutboxService? _integrationEventService;

    /// <summary>
    /// Constructor for TransactionBehavior with integration events.
    /// </summary>
    /// <param name="dbContext">The database context for the bounded context</param>
    /// <param name="integrationEventService">Service for managing integration events (optional)</param>
    /// <param name="logger">Logger instance</param>
    public TransactionBehavior(
        QltDBContextBase dbContext,
        ILogger<TransactionBehavior<TRequest, TResponse>> logger,
        IEventBusOutboxService? integrationEventService = null)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _integrationEventService = integrationEventService;
    }

    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        var response = default(TResponse);
        var typeName = request.GetGenericTypeName();

        using var activity = BehaviorHelper.StartTransactionActivity(typeName);

        try
        {
            if (_dbContext.HasActiveTransaction)
            {
                BehaviorHelper.SetNestedTransactionTag(activity);
                return await next(cancellationToken);
            }

            var strategy = _dbContext.Database.CreateExecutionStrategy();
            var stopwatch = Stopwatch.StartNew();

            await strategy.ExecuteAsync(async () =>
            {
                Guid transactionId;

                await using var transaction = await _dbContext.BeginTransactionAsync();
                using (_logger.BeginScope(new List<KeyValuePair<string, object>> { new("TransactionContext", transaction.TransactionId) }))
                {
                    BehaviorHelper.SetTransactionId(activity, transaction.TransactionId);

                    _logger.LogInformation("Begin transaction {TransactionId} for {CommandName} ({@Command})", transaction.TransactionId, typeName, request);

                    response = await next(cancellationToken);

                    _logger.LogInformation("Commit transaction {TransactionId} for {CommandName}", transaction.TransactionId, typeName);

                    await _dbContext.CommitTransactionAsync(transaction);

                    transactionId = transaction.TransactionId;
                }
                // Publish integration events AFTER successful commit (Outbox Pattern)
                if (_integrationEventService != null)
                {
                    await _integrationEventService.PublishEventsThroughEventBusAsync(transactionId);
                }
            });

            stopwatch.Stop();
            var elapsedMs = stopwatch.Elapsed.TotalMilliseconds;

            BehaviorHelper.RecordTransactionSuccess(activity, typeName, elapsedMs);

            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error Handling transaction for {CommandName} ({@Command})", typeName, request);

            BehaviorHelper.RecordTransactionFailure(activity, typeName, ex);

            throw;
        }
    }
}
