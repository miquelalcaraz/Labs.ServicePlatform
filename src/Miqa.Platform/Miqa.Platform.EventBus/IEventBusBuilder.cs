using Microsoft.Extensions.DependencyInjection;

namespace Miqa.Platform.EventBus;


public interface IEventBusBuilder
{
    public IServiceCollection Services { get; }
}
