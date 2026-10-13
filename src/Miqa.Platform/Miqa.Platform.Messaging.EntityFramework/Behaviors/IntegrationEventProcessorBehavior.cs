using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

using MediatR;

using Microsoft.Extensions.Logging;

using Miqa.Platform.Activities;
using Miqa.Platform.Extensions;
using Miqa.Platform.Messaging.Services;
using Miqa.Platform.Persistence;

namespace Miqa.Platform.Messaging.Behaviors;

/// <summary>
/// Pipeline behavior that publishes integration events from the outbox after the transaction commits.
/// Must be registered OUTSIDE TransactionBehavior so it runs after commit.
/// Reads the committed transaction ID from ITransactionalUnitOfWork to determine which events to publish.
/// </summary>
public class IntegrationEventProcessorBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private readonly ITransactionalUnitOfWork _unitOfWork;
    private readonly ILogger<IntegrationEventProcessorBehavior<TRequest, TResponse>> _logger;
    private readonly IEventBusOutboxService? _outboxService;

    public IntegrationEventProcessorBehavior(
        ITransactionalUnitOfWork unitOfWork,
        ILogger<IntegrationEventProcessorBehavior<TRequest, TResponse>> logger,
        IEventBusOutboxService? outboxService = null)
    {
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _outboxService = outboxService;
    }

    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        var response = await next(cancellationToken);
        var transactionId = _unitOfWork.GetCurrentTransaction()?.TransactionId ?? _unitOfWork.LastCommittedTransactionId;

        if (!transactionId.HasValue)
        {
            return response;
        }
        var typeName = typeof(TRequest).GetGenericTypeName();

        using var activity = BehaviorHelper.StartIntegrationEventProcessorActivity(typeName);
        BehaviorHelper.SetProcessorTransactionId(activity, transactionId.Value);

        var stopwatch = Stopwatch.StartNew();

        if (_outboxService is not null)
        {
            _logger.LogInformation(
                "Publishing integration events for transaction {TransactionId} after {CommandName}",
                transactionId,
                typeName);

            await _outboxService.PublishEventsThroughEventBusAsync(transactionId.Value);
        }

        stopwatch.Stop();
        BehaviorHelper.RecordIntegrationEventProcessorComplete(activity, transactionId.Value, stopwatch.Elapsed.TotalMilliseconds);

        return response;
    }
}
