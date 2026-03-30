using MediatR;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using Qlt.Platform.Activities;
using Qlt.Platform.Extensions;

using System.Diagnostics;

namespace Qlt.Platform.Persistence.Behaviors;

/// <summary>
/// Pipeline behavior that wraps command execution in a database transaction with retry support.
/// </summary>
public class TransactionBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private readonly ILogger<TransactionBehavior<TRequest, TResponse>> _logger;
    private readonly ITransactionalUnitOfWork _dbTransaction;

    public TransactionBehavior(
        ITransactionalUnitOfWork dbTransaction,
        ILogger<TransactionBehavior<TRequest, TResponse>> logger)
    {
        _dbTransaction = dbTransaction ?? throw new ArgumentNullException(nameof(dbTransaction));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        var response = default(TResponse);
        var typeName = request.GetGenericTypeName();

        using var activity = BehaviorHelper.StartTransactionActivity(typeName);

        try
        {
            if (_dbTransaction.HasActiveTransaction)
            {
                BehaviorHelper.SetNestedTransactionTag(activity);
                return await next(cancellationToken);
            }

            var strategy = _dbTransaction.CreateExecutionStrategy();
            var stopwatch = Stopwatch.StartNew();

            await strategy.ExecuteAsync(async () =>
            {
                await using var transaction = await _dbTransaction.BeginTransactionAsync();
                using (_logger.BeginScope(new List<KeyValuePair<string, object>> { new("TransactionContext", transaction.TransactionId) }))
                {
                    BehaviorHelper.SetTransactionId(activity, transaction.TransactionId);

                    _logger.LogInformation("Begin transaction {TransactionId} for {CommandName} ({@Command})", transaction.TransactionId, typeName, request);

                    response = await next(cancellationToken);

                    _logger.LogInformation("Commit transaction {TransactionId} for {CommandName}", transaction.TransactionId, typeName);

                    await _dbTransaction.CommitTransactionAsync(transaction);
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
