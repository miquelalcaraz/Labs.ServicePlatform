using MediatR;

using Sirius.Domain.SeedWork;

namespace Sirius.Platform.Messaging.DomainEvents
{
    /// <summary>
    /// Handler for domain events. Adapts Domain.IDomainEvent to MediatR.INotificationHandler.
    /// This interface bridges the domain layer (pure DDD) with the messaging infrastructure (MediatR).
    /// </summary>
    public interface IDomainEventHandler<TEvent> : INotificationHandler<TEvent>
        where TEvent : IDomainEvent, INotification
    { }

}
