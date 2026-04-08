using MediatR;

using Qlt.Domain.SeedWork;
using Qlt.Platform.Activities;

using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Qlt.Platform.Messaging.Extensions;

public static class DomainEventsDispatcherExtension
{
    /// <summary>
    /// Dispatches domain events from entities with pending events.
    /// Extracts events, clears them from entities, and publishes via MediatR with telemetry.
    /// Called by DomainEventProcessorBehavior within the active transaction before commit.
    /// </summary>
    public static async Task DispatchDomainEventsAsync(
        this IMediator mediator,
        IReadOnlyList<Entity> entities,
        CancellationToken cancellationToken)
    {
        var domainEvents = entities
            .Where(x => x.DomainEvents != null && x.DomainEvents.Any())
            .SelectMany(x => x.DomainEvents!)
            .ToList();

        if (domainEvents.Count == 0)
        {
            return;
        }

        entities
            .Where(x => x.DomainEvents != null)
            .ToList()
            .ForEach(entity => entity.ClearDomainEvents());

        using var activity = BehaviorHelper.StartDomainEventDispatchActivity(domainEvents.Count, entities.Count);

        foreach (var domainEvent in domainEvents)
        {
            BehaviorHelper.RecordDomainEventDispatched(domainEvent.GetType().Name);
            await mediator.Publish(domainEvent, cancellationToken);
        }

        BehaviorHelper.SetDomainEventDispatchComplete(activity);
    }
}
