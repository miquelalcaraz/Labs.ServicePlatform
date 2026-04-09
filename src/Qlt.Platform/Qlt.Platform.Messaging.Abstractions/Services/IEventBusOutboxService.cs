using Qlt.Platform.Messaging.IntegrationEvents;

using System;
using System.Threading.Tasks;

namespace Qlt.Platform.Messaging.Services;

/// <summary>
/// Generic interface for managing integration events in any bounded context.
/// Implementations handle storing events in the outbox pattern and publishing them.
/// </summary>
public interface IEventBusOutboxService
{
    /// <summary>
    /// Adds an integration event to the outbox change tracker within the current transaction.
    /// The event is persisted when the transaction commits via CommitTransactionAsync.
    /// </summary>
    /// <param name="evt">The integration event to store</param>
    Task AddAndSaveEventAsync(IntegrationEvent evt);

    /// <summary>
    /// Publishes all pending integration events for a specific transaction to the event bus.
    /// This is called AFTER the transaction commits successfully (Outbox Pattern).
    /// </summary>
    /// <param name="transactionId">The database transaction ID</param>
    Task PublishEventsThroughEventBusAsync(Guid transactionId);
}
