using Sirius.Domain.SeedWork;

namespace Sirius.Platform.Persistence;

/// <summary>
/// Provides access to tracked entities that may have pending domain events.
/// Used by DomainEventProcessorBehavior to discover and dispatch domain events within the active transaction.
/// </summary>
public interface IDomainEntityChangeTracker
{
    /// <summary>
    /// Gets all tracked entities from the change tracker.
    /// Callers are responsible for filtering entities with pending domain events.
    /// </summary>
    /// <returns>Read-only list of tracked entities.</returns>
    IReadOnlyList<Entity> GetEntitiesWithChanges();
}
