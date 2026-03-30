using Qlt.Domain.SeedWork;

using System.Collections.Generic;

namespace Qlt.Platform.Messaging;

/// <summary>
/// Collects entities with pending domain events during SaveEntitiesAsync.
/// Events remain in entities until dispatched by DomainEventsDispatcherExtension after transaction commit.
/// </summary>
public interface IDomainEventCollector
{
    /// <summary>
    /// Gets entities that have pending domain events collected during SaveEntitiesAsync.
    /// Events are extracted and dispatched by DomainEventsDispatcherExtension after commit.
    /// Calling this method clears the stored entities list.
    /// </summary>
    /// <returns>Read-only list of entities with pending domain events.</returns>
    IReadOnlyList<Entity> GetEntitiesWithPendingEvents();
}
