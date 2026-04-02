using System.Threading;
using System.Threading.Tasks;

using Qlt.Platform.Messaging.IntegrationEvents;



namespace Qlt.Platform.EventBus;

public interface IEventBus
{
    Task PublishAsync(IntegrationEvent @event, CancellationToken cancellationToken = default);
}
