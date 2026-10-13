using System;
using System.Diagnostics;
using System.Threading.Tasks;

using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;

using Miqa.Platform.Activities;
using Miqa.Platform.Messaging.Models;
using Miqa.Platform.Messaging.Services;
using Miqa.Platform.Persistence;

namespace Miqa.Platform.Messaging.IntegrationEvents;

/// <summary>
/// Decorator that enforces idempotency for integration event handlers with transactional guarantees using Entity Framework.
/// </summary>
public class IdempotencyIntegrationEventEfDecorator<TIntegrationEvent> : IIntegrationEventHandler<TIntegrationEvent>, IDecoratedHandler<TIntegrationEvent>
    where TIntegrationEvent : IntegrationEvent
{
    private readonly IIntegrationEventHandler<TIntegrationEvent> _inner;
    private readonly IIdempotencyService _idempotencyService;
    private readonly ILogger _logger;
    private readonly ITransactionalUnitOfWork? _unitOfWork;

    public IIntegrationEventHandler<TIntegrationEvent> InnerHandler => _inner;

    public IdempotencyIntegrationEventEfDecorator(
        IIntegrationEventHandler<TIntegrationEvent> inner,
        IIdempotencyService idempotencyService,
        ILogger logger,
        ITransactionalUnitOfWork? unitOfWork = null)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        _idempotencyService = idempotencyService ?? throw new ArgumentNullException(nameof(idempotencyService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _unitOfWork = unitOfWork;
    }

    public async Task Handle(TIntegrationEvent @event)
    {
        var handlerName = GetActualHandlerName(_inner);
        var eventType = typeof(TIntegrationEvent).Name;
        var key = IdempotencyKey.ForEvent(@event.Id, handlerName);

        using var activity = IdempotencyHelper.StartEventIdempotencyCheck(@event.Id, handlerName);

        var stopwatch = Stopwatch.StartNew();

        var alreadyProcessed = await _idempotencyService.ExistsAsync(key);

        stopwatch.Stop();
        var checkDurationMs = stopwatch.Elapsed.TotalMilliseconds;

        IdempotencyHelper.RecordCheckResult(activity, alreadyProcessed, checkDurationMs);
        IdempotencyHelper.RecordCheckDurationMetric("event", alreadyProcessed, checkDurationMs);

        if (alreadyProcessed)
        {
            IdempotencyHelper.RecordDuplicateEvent(activity, @event.Id, handlerName);

            _logger.LogInformation(
                "Duplicate integration event detected: {EventType} with ID {EventId} already processed by {HandlerName}. Skipping.",
                eventType,
                @event.Id,
                handlerName);

            return;
        }

        _logger.LogInformation(
            "Processing integration event: {EventType} with ID {EventId} using {HandlerName}",
            eventType,
            @event.Id,
            handlerName);

        if (_unitOfWork != null)
        {
            await ProcessWithTransactionAsync(@event, key, eventType);
        }
        else
        {
            await ProcessWithoutTransactionAsync(@event, key, eventType);
        }
    }

    private async Task ProcessWithTransactionAsync(TIntegrationEvent @event, IdempotencyKey key, string eventType)
    {
        var shouldManageTransaction = !_unitOfWork!.HasActiveTransaction;
        IDbContextTransaction? transaction = null;

        try
        {
            if (shouldManageTransaction)
            {
                transaction = await _unitOfWork.BeginTransactionAsync();
            }

            await _inner.Handle(@event);

            await _idempotencyService.MarkAsProcessingAsync(key, eventType);

            await _unitOfWork.SaveChangesAsync();

            if (shouldManageTransaction && transaction is not null)
            {
                await _unitOfWork.CommitTransactionAsync(transaction);
            }

            await _idempotencyService.MarkAsCompletedAsync(key);

            _logger.LogInformation(
                "Successfully processed and marked integration event: {EventType} with ID {EventId} by {HandlerName}",
                eventType,
                @event.Id,
                key.Qualifier);
        }
        catch (Exception ex)
        {
            if (shouldManageTransaction && _unitOfWork.HasActiveTransaction)
            {
                _unitOfWork.RollbackTransaction();
            }

            _logger.LogError(ex,
                "Failed to process integration event: {EventType} with ID {EventId} by {HandlerName}. Transaction rolled back, event NOT marked as processed.",
                eventType,
                @event.Id,
                key.Qualifier);

            throw;
        }
    }

    private async Task ProcessWithoutTransactionAsync(TIntegrationEvent @event, IdempotencyKey key, string eventType)
    {
        try
        {
            await _inner.Handle(@event);

            await _idempotencyService.MarkAsProcessingAsync(key, eventType);
            await _idempotencyService.MarkAsCompletedAsync(key);

            _logger.LogInformation(
                "Successfully processed and marked integration event: {EventType} with ID {EventId} by {HandlerName}",
                eventType,
                @event.Id,
                key.Qualifier);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to process integration event: {EventType} with ID {EventId} by {HandlerName}. Event NOT marked as processed and will be retried.",
                eventType,
                @event.Id,
                key.Qualifier);

            throw;
        }
    }

    private static string GetActualHandlerName(IIntegrationEventHandler<TIntegrationEvent> handler)
    {
        while (handler is IDecoratedHandler<TIntegrationEvent> decorator)
            handler = decorator.InnerHandler;

        return handler.GetType().Name;
    }
}
