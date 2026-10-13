using System.Threading;
using System.Threading.Tasks;

using Miqa.Platform.Messaging.IntegrationEvents;



namespace Miqa.Platform.EventBus;

public interface IEventBus
{
    Task PublishAsync(IntegrationEvent @event, CancellationToken cancellationToken = default);
}
