using Qlt.Platform.Messaging.IntegrationEvents;

using System.Threading.Tasks;



namespace Qlt.Platform.EventBus.Abstractions;

public interface IEventBus
{
    Task PublishAsync(IntegrationEvent @event);
}
