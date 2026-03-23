using MediatR;

namespace QltS.Shared.Messaging.DomainEvents
{
    public interface IDomainEventHandler<TEvent> : INotificationHandler<TEvent>
        where TEvent : IDomainEvent
    { }

}
