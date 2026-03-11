using MediatR;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using QltSystem.Core.Application.Services;
using QltSystem.Core.EventBus.Extensions;

namespace QltSystem.Core.Application.Behaviors;

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
    private readonly DbContext _dbContext;
    private readonly IIntegrationEventService? _integrationEventService;

    /// <summary>
    /// Constructor for TransactionBehavior with integration events.
    /// </summary>
    /// <param name="dbContext">The database context for the bounded context</param>
    /// <param name="integrationEventService">Service for managing integration events (optional)</param>
    /// <param name="logger">Logger instance</param>
    public TransactionBehavior(
        DbContext dbContext,
        ILogger<TransactionBehavior<TRequest, TResponse>> logger,
        IIntegrationEventService? integrationEventService = null)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _integrationEventService = integrationEventService;
    }

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var response = default(TResponse);
        var typeName = request.GetGenericTypeName();

        try
        {
            // If already in a transaction, don't create a new one
            if (_dbContext.Database.CurrentTransaction != null)
            {
                return await next();
            }

            var strategy = _dbContext.Database.CreateExecutionStrategy();

            await strategy.ExecuteAsync(async () =>
            {
                Guid transactionId;

                await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);
                
                using (_logger.BeginScope(new Dictionary<string, object>
                {
                    ["TransactionContext"] = transaction.TransactionId
                }))
                {
                    _logger.LogInformation(
                        "BEGIN transaction {TransactionId} for {CommandName}",
                        transaction.TransactionId,
                        typeName);

                    // Execute the command handler
                    response = await next();

                    _logger.LogInformation(
                        "COMMIT transaction {TransactionId} for {CommandName}",
                        transaction.TransactionId,
                        typeName);

                    // Commit the transaction
                    await transaction.CommitAsync(cancellationToken);

                    transactionId = transaction.TransactionId;
                }

                // Publish integration events AFTER successful commit (Outbox Pattern)
                if (_integrationEventService != null)
                {
                    _logger.LogDebug(
                        "Publishing integration events for transaction {TransactionId}",
                        transactionId);

                    await _integrationEventService.PublishEventsThroughEventBusAsync(transactionId);
                }
            });

            return response!;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "ERROR handling transaction for {CommandName} ({@Command})",
                typeName,
                request);

            throw;
        }
    }
}
