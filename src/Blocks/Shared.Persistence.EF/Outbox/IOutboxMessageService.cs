

using Microsoft.EntityFrameworkCore.Storage;

using QltS.Shared.Messaging.IntegrationEvents;
using QltS.Shared.Messaging.Outbox;

namespace QltS.Shared.Persistence.EF.Outbox;

/// <summary>
/// Service for managing integration events in the transactional outbox.
/// Implements the Transactional Outbox pattern to ensure reliable event publishing.
/// </summary>
public interface IOutboxMessageService
{
    /// <summary>
    /// Retrieves all events that are pending publication for a specific transaction.
    /// </summary>
    /// <param name="transactionId">The transaction identifier</param>
    /// <returns>Collection of pending integration events</returns>
    Task<IEnumerable<OutboxMessage>> RetrieveEventLogsPendingToPublishAsync(Guid transactionId);

    /// <summary>
    /// Saves an integration event to the outbox within a transaction.
    /// </summary>
    /// <param name="event">The integration event to save</param>
    /// <param name="transaction">The active database transaction</param>
    Task SaveEventAsync(IntegrationEvent @event, IDbContextTransaction transaction);

    /// <summary>
    /// Marks an event as successfully published.
    /// </summary>
    /// <param name="eventId">The event identifier</param>
    Task MarkEventAsPublishedAsync(Guid eventId);

    /// <summary>
    /// Marks an event as in progress (currently being published).
    /// </summary>
    /// <param name="eventId">The event identifier</param>
    Task MarkEventAsInProgressAsync(Guid eventId);

    /// <summary>
    /// Marks an event as failed to publish.
    /// </summary>
    /// <param name="eventId">The event identifier</param>
    Task MarkEventAsFailedAsync(Guid eventId);
}
