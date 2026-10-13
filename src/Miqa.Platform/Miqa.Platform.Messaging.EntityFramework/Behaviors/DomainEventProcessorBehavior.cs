using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using MediatR;

using Microsoft.Extensions.Logging;

using Miqa.Platform.Activities;
using Miqa.Platform.Extensions;
using Miqa.Platform.Messaging.Extensions;
using Miqa.Platform.Messaging.Services;
using Miqa.Platform.Persistence;

namespace Miqa.Platform.Messaging.Behaviors;

/// <summary>
/// Pipeline behavior that dispatches domain events from tracked entities within the active transaction.
/// Must be registered INSIDE TransactionBehavior so domain event handlers participate in the same transaction.
/// Domain event handlers can modify aggregates and register integration events to the outbox.
/// Supports cascading: re-checks for new domain events after each dispatch cycle.
/// </summary>
public class DomainEventProcessorBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private readonly ITransactionalUnitOfWork _unitOfWork;
    private readonly ILogger<DomainEventProcessorBehavior<TRequest, TResponse>> _logger;
    private readonly IEventBusOutboxService? _outboxService;
    private readonly IMediator _mediator;
    private readonly IDomainEntityChangeTracker? _eventCollector;

    public DomainEventProcessorBehavior(
        ITransactionalUnitOfWork unitOfWork,
        ILogger<DomainEventProcessorBehavior<TRequest, TResponse>> logger,
        IMediator mediator,
        IEventBusOutboxService? outboxService = null,
        IDomainEntityChangeTracker? eventCollector = null)
    {
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
        _outboxService = outboxService;
        _eventCollector = eventCollector;
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

        using var activity = BehaviorHelper.StartDomainEventProcessorActivity(typeName);
        BehaviorHelper.SetProcessorTransactionId(activity, transactionId.Value);

        var stopwatch = Stopwatch.StartNew();
        var dispatchCycles = 0;
        var totalEntityCount = 0;

        if (_eventCollector is not null)
        {
            var entities = _eventCollector.GetEntitiesWithChanges().Where(x => x.DomainEvents != null && x.DomainEvents.Any()).ToList();
            while (entities.Count > 0)
            {
                dispatchCycles++;
                totalEntityCount += entities.Count;

                _logger.LogInformation(
                    "Dispatching domain events from {Count} entities after transaction {TransactionId} for {CommandName}",
                    entities.Count,
                    transactionId,
                    typeName);

                await _mediator.DispatchDomainEventsAsync(entities, cancellationToken);
                entities = _eventCollector.GetEntitiesWithChanges().Where(x => x.DomainEvents != null && x.DomainEvents.Any()).ToList();
            }
        }

        stopwatch.Stop();
        BehaviorHelper.RecordDomainEventProcessorComplete(activity, totalEntityCount, dispatchCycles, stopwatch.Elapsed.TotalMilliseconds);

        return response;
    }
}
