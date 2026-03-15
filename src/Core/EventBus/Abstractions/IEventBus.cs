using QltSystem.Core.EventBus.Contracts.Events;

using System.Threading.Tasks;

namespace QltSystem.Core.EventBus.Abstractions;

public interface IEventBus
{
    Task PublishAsync(IntegrationEvent @event);
}
