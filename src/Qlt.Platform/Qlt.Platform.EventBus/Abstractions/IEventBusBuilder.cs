using Microsoft.Extensions.DependencyInjection;

namespace Qlt.Platform.EventBus.Abstractions;


public interface IEventBusBuilder
{
    public IServiceCollection Services { get; }
}
