using Microsoft.Extensions.DependencyInjection;

namespace QltS.Shared.EventBus.Abstractions;


public interface IEventBusBuilder
{
    public IServiceCollection Services { get; }
}
