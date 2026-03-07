using QltSystem.Core.EventBus.Events;

namespace QltSystem.Accounts.Application.IntegrationEvents;

public interface IAccountIntegrationEventService
{
    Task PublishEventsThroughEventBusAsync(Guid transactionId);
    Task AddAndSaveEventAsync(IntegrationEvent evt);
}
