using MediatR;

namespace QltS.Core.Domain.Messaging
{
    public interface IDomainEventHandler<TEvent> : INotificationHandler<TEvent>
        where TEvent : IDomainEvent
    { }

}
