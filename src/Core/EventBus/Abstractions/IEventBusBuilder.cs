using Microsoft.Extensions.DependencyInjection;

namespace QltS.Core.EventBus.Abstractions;


public interface IEventBusBuilder
{
    public IServiceCollection Services { get; }
}
