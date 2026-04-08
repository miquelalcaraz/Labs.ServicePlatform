using Qlt.Platform.Messaging.IntegrationEvents;
using Qlt.Platform.Messaging.Models;

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Qlt.Platform.Messaging.Services;

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
    /// Saves an integration event to the outbox within the current transaction scope.
    /// </summary>
    /// <param name="event">The integration event to save</param>
    /// <param name="transactionId">The active transaction identifier</param>
    Task SaveEventAsync(IntegrationEvent @event, Guid transactionId);

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

    /// <summary>
    /// Retrieves unpublished or previously failed events for background processing.
    /// Returns events with status NotPublished or FailedToPublish that have not exceeded the maximum retry count.
    /// </summary>
    /// <param name="batchSize">Maximum number of events to retrieve</param>
    /// <param name="maxRetryCount">Maximum number of publish attempts before giving up</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task<IEnumerable<OutboxMessage>> RetrieveUnpublishedEventsAsync(int batchSize, int maxRetryCount, CancellationToken cancellationToken = default);
}
