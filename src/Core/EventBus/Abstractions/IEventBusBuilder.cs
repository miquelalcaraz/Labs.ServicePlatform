using Microsoft.Extensions.DependencyInjection;

namespace QltSystem.Core.EventBus.Abstractions;


public interface IEventBusBuilder
{
    public IServiceCollection Services { get; }
}
