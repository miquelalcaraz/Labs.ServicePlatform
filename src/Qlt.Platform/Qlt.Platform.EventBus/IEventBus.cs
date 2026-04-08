using Qlt.Platform.Messaging.IntegrationEvents;

using System.Threading;
using System.Threading.Tasks;



namespace Qlt.Platform.EventBus;

public interface IEventBus
{
    Task PublishAsync(IntegrationEvent @event, CancellationToken cancellationToken = default);
}
