using System.Threading;
using System.Threading.Tasks;

using Sirius.Platform.Messaging.IntegrationEvents;



namespace Sirius.Platform.EventBus;

public interface IEventBus
{
    Task PublishAsync(IntegrationEvent @event, CancellationToken cancellationToken = default);
}
