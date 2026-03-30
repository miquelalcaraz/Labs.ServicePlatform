namespace Qlt.Platform.Messaging.IntegrationEvents;

/// <summary>
/// Interface for decorator handlers that wrap an inner handler.
/// Enables unwrapping decorator chains without reflection.
/// </summary>
public interface IDecoratedHandler<TEvent> where TEvent : IntegrationEvent
{
    IIntegrationEventHandler<TEvent> InnerHandler { get; }
}
