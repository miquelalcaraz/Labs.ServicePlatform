using QltS.Shared.Messaging.IntegrationEvents;

using System.Threading.Tasks;



namespace QltS.Shared.EventBus.Abstractions;

public interface IEventBus
{
    Task PublishAsync(IntegrationEvent @event);
}
