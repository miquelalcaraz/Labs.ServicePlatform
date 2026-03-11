using MediatR;

namespace QltSystem.Core.Domain.Messaging
{
    public interface IDomainEventHandler<TEvent> : INotificationHandler<TEvent>
        where TEvent : IDomainEvent
    { }

}
