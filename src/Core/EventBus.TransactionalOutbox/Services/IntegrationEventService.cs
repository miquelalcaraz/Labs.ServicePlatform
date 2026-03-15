using Microsoft.Extensions.Logging;

using QltSystem.Core.EventBus.Abstractions;
using QltSystem.Core.EventBus.Contracts.Events;
using QltSystem.Core.EventBus.Services;
using QltSystem.Core.Persistence.EF;

namespace QltSystem.Core.EventBus.TransactionalOutbox.Services;

/// <summary>
/// Service that combines the outbox pattern with event publishing.
/// Coordinates saving events to the outbox and publishing them through the event bus.
/// </summary>
public class IntegrationEventService<TContext> : IIntegrationEventService
    where TContext : DBContextBase
{
    private readonly IEventBus _eventBus;
    private readonly TContext _dbContext;
    private readonly IIntegrationEventLogService _eventLogService;
    private readonly ILogger<IIntegrationEventLogService> _logger;

    public IntegrationEventService(
        IEventBus eventBus,
        TContext dbContext,
        IIntegrationEventLogService integrationEventLogService,
        ILogger<IIntegrationEventLogService> logger)
    {
        _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _eventLogService = integrationEventLogService ?? throw new ArgumentNullException(nameof(integrationEventLogService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task PublishEventsThroughEventBusAsync(Guid transactionId)
    {
        var pendingLogEvents = await _eventLogService.RetrieveEventLogsPendingToPublishAsync(transactionId);

        foreach (var logEvt in pendingLogEvents)
        {
            _logger.LogInformation("Publishing integration event: {IntegrationEventId} - ({@IntegrationEvent})", logEvt.EventId, logEvt.IntegrationEvent);

            try
            {
                await _eventLogService.MarkEventAsInProgressAsync(logEvt.EventId);
                await _eventBus.PublishAsync(logEvt.IntegrationEvent);
                await _eventLogService.MarkEventAsPublishedAsync(logEvt.EventId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error publishing integration event: {IntegrationEventId}", logEvt.EventId);

                await _eventLogService.MarkEventAsFailedAsync(logEvt.EventId);
            }
        }
    }

    public async Task AddAndSaveEventAsync(IntegrationEvent evt)
    {
        _logger.LogInformation("Enqueuing integration event {IntegrationEventId} to repository ({@IntegrationEvent})", evt.Id, evt);

        await _eventLogService.SaveEventAsync(evt, _dbContext.GetCurrentTransaction());
    }
}
