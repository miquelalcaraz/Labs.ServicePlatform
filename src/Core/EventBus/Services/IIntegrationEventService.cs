using QltSystem.Core.EventBus.Contracts.Events;

using System;
using System.Threading.Tasks;

namespace QltSystem.Core.EventBus.Services;

/// <summary>
/// Generic interface for managing integration events in any bounded context.
/// Implementations handle storing events in the outbox pattern and publishing them.
/// </summary>
public interface IIntegrationEventService
{
    /// <summary>
    /// Adds an integration event to the outbox and saves it to the database.
    /// This happens within the same transaction as the aggregate changes.
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
