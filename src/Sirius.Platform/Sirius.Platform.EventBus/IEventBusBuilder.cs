using Microsoft.Extensions.DependencyInjection;

namespace Sirius.Platform.EventBus;


public interface IEventBusBuilder
{
    public IServiceCollection Services { get; }
}
