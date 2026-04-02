using Microsoft.Extensions.DependencyInjection;

namespace Qlt.Platform.EventBus;


public interface IEventBusBuilder
{
    public IServiceCollection Services { get; }
}
