using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

using MediatR;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using Sirius.Platform.Activities;
using Sirius.Platform.Extensions;
using Sirius.Platform.Messaging.Commands;
using Sirius.Platform.Persistence;

namespace Sirius.Platform.Messaging.Behaviors;

/// <summary>
/// Pipeline behavior that wraps command execution in a database transaction with retry support.
/// Only activates for requests implementing <see cref="ICommandRequest"/>; queries and other request types pass through without a transaction.
/// Commits only when the response is not a failed <see cref="Result"/>; a failed result rolls the transaction back.
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
        if (request is not ICommandRequest)
            return await next(cancellationToken);

        var response = default(TResponse);
        var typeName = request.GetGenericTypeName();
        var entitiesAffected = 0;
        var rolledBackErrorCode = default(string);

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

                    if (response is Result { IsFailure: true } failedResult)
                    {
                        rolledBackErrorCode = failedResult.Error.Code;

                        _logger.LogInformation(
                            "Rollback transaction {TransactionId} for {CommandName} because the command returned a failure ({ErrorCode})",
                            transaction.TransactionId, typeName, rolledBackErrorCode);

                        await transaction.RollbackAsync(cancellationToken);
                        return;
                    }

                    entitiesAffected = _dbTransaction.GetPendingChangesCount();

                    _logger.LogInformation("Commit transaction {TransactionId} for {CommandName}", transaction.TransactionId, typeName);

                    await _dbTransaction.CommitTransactionAsync(transaction);
                }
            });

            stopwatch.Stop();
            var elapsedMs = stopwatch.Elapsed.TotalMilliseconds;

            if (rolledBackErrorCode is not null)
                BehaviorHelper.RecordTransactionRollback(activity, typeName, elapsedMs, rolledBackErrorCode);
            else
                BehaviorHelper.RecordTransactionSuccess(activity, typeName, elapsedMs, entitiesAffected);

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
