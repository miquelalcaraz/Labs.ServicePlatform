using MediatR;

using Microsoft.EntityFrameworkCore;

using QltS.Core.Domain.SeedWork;
using QltS.Shared.Telemetry.Meters;

using System.Diagnostics;

namespace QltS.Shared.Persistence.EF.Extensions;

public static class DomainEventsDispatcherExtension
{
    public static async Task DispatchDomainEventsAsync(this IMediator mediator, DbContext ctx, CancellationToken cancellationToken)
    {
        var domainEntities = ctx.ChangeTracker
            .Entries<Entity>()
            .Where(x => x.Entity.DomainEvents != null && x.Entity.DomainEvents.Any());

        var domainEvents = domainEntities
            .SelectMany(x => x.Entity.DomainEvents!)
            .ToList();

        domainEntities.ToList()
            .ForEach(entity => entity.Entity.ClearDomainEvents());

        if (domainEvents.Count == 0)
        {
            return;
        }

        // Start activity for domain events dispatching
        using var activity = ApplicationDiagnostics.ActivitySource.StartActivity(
            name: "Dispatch Domain Events",
            kind: ActivityKind.Internal);

        activity?.SetTag("domain_events.count", domainEvents.Count);

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
}
