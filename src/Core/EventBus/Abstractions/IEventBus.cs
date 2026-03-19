using System.Threading.Tasks;

using QltS.Core.EventBus.Contracts.Events;

namespace QltS.Core.EventBus.Abstractions;

public interface IEventBus
{
    Task PublishAsync(IntegrationEvent @event);
}
