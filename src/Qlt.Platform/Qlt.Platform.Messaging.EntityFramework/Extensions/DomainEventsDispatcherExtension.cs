using MediatR;

using Microsoft.EntityFrameworkCore;

using Qlt.Domain.SeedWork;
using Qlt.Platform.Meters;

using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Qlt.Platform.Messaging.Extensions;

public static class DomainEventsDispatcherExtension
{
    /// <summary>
    /// Dispatches domain events from entities with pending events.
    /// Extracts events, clears them from entities, and publishes via MediatR with telemetry.
    /// This is the primary method called by MessagingBehavior after transaction commit.
    /// </summary>
    public static async Task DispatchDomainEventsAsync(
        this IMediator mediator, 
        IReadOnlyList<Entity> entities, 
        CancellationToken cancellationToken)
    {
        // Extract events from entities (AFTER commit)
        var domainEvents = entities
            .Where(x => x.DomainEvents != null && x.DomainEvents.Any())
            .SelectMany(x => x.DomainEvents!)
            .ToList();

        if (domainEvents.Count == 0)
        {
            return;
        }

        // Clear events from entities (now safe after commit)
        entities
            .Where(x => x.DomainEvents != null)
            .ToList()
            .ForEach(entity => entity.ClearDomainEvents());

        // Start activity for domain events dispatching
        using var activity = ApplicationDiagnostics.ActivitySource.StartActivity(
            name: "Dispatch Domain Events",
            kind: ActivityKind.Internal);

        activity?.SetTag("domain_events.count", domainEvents.Count);
        activity?.SetTag("entities.count", entities.Count);

        foreach (var domainEvent in domainEvents)
        {
            var eventType = domainEvent.GetType().Name;

            // Record domain event publication metric
            ApplicationDiagnostics.DomainEventsPublished.Add(1,
                new KeyValuePair<string, object?>("event_type", eventType));

            await mediator.Publish(domainEvent, cancellationToken);
        }

        activity?.SetTag("domain_events.dispatched", true);
    }

    /// <summary>
    /// Dispatches domain events from DbContext ChangeTracker.
    /// Kept for backward compatibility. Prefer using the overload with IReadOnlyList&lt;Entity&gt;.
    /// </summary>
    public static async Task DispatchDomainEventsAsync(
        this IMediator mediator, 
        DbContext ctx, 
        CancellationToken cancellationToken)
    {
        var domainEntities = ctx.ChangeTracker
            .Entries<Entity>()
            .Where(x => x.Entity.DomainEvents != null && x.Entity.DomainEvents.Any())
            .Select(x => x.Entity)
            .ToList();

        await mediator.DispatchDomainEventsAsync(domainEntities, cancellationToken);
    }
}
