using System.Threading.Tasks;

using QltSystem.Core.EventBus.Events;

namespace QltSystem.Core.EventBus.Abstractions;

public interface IEventBus
{
    Task PublishAsync(IntegrationEvent @event);
}
