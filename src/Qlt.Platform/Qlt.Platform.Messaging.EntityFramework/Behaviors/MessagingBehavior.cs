using MediatR;

using Microsoft.Extensions.Logging;

using Qlt.Platform.Extensions;
using Qlt.Platform.Messaging.Extensions;
using Qlt.Platform.Messaging.Outbox;
using Qlt.Platform.Persistence;

using System;
using System.Threading;
using System.Threading.Tasks;

namespace Qlt.Platform.Messaging.Behaviors;

/// <summary>
/// Pipeline behavior that dispatches domain events and publishes integration events after the transaction commits.
/// Domain events are dispatched first (handlers publish integration events to outbox), then integration events are published to the event bus.
/// Reads the committed transaction ID from ITransactionalUnitOfWork to determine which events to publish.
/// </summary>
public class MessagingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private readonly ITransactionalUnitOfWork _unitOfWork;
    private readonly ILogger<MessagingBehavior<TRequest, TResponse>> _logger;
    private readonly IEventBusOutboxService? _outboxService;
    private readonly IMediator _mediator;
    private readonly IDomainEventCollector? _eventCollector;

    public MessagingBehavior(
        ITransactionalUnitOfWork unitOfWork,
        ILogger<MessagingBehavior<TRequest, TResponse>> logger,
        IMediator mediator,
        IEventBusOutboxService? outboxService = null,
        IDomainEventCollector? eventCollector = null)
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

        if (_unitOfWork.LastCommittedTransactionId is { } transactionId)
        {
            var typeName = typeof(TRequest).GetGenericTypeName();

            // 1. Dispatch Domain Events first (handlers will publish integration events to outbox)
            if (_eventCollector is not null)
            {
                var entities = _eventCollector.GetEntitiesWithPendingEvents();
                if (entities.Count > 0)
                {
                    _logger.LogInformation(
                        "Dispatching domain events from {Count} entities after transaction {TransactionId} for {CommandName}",
                        entities.Count,
                        transactionId,
                        typeName);

                    await _mediator.DispatchDomainEventsAsync(entities, cancellationToken);
                }
            }

            // 2. Publish Integration Events to event bus
            if (_outboxService is not null)
            {
                _logger.LogInformation(
                    "Publishing integration events for transaction {TransactionId} after {CommandName}",
                    transactionId,
                    typeName);

                await _outboxService.PublishEventsThroughEventBusAsync(transactionId);
            }
        }

        return response;
    }
}
