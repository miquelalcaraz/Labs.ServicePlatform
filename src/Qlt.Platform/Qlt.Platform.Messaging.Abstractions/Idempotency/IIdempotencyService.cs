using System.Threading.Tasks;

namespace Qlt.Platform.Messaging.Idempotency;

/// <summary>
/// Unified idempotency service for both command requests and integration events.
/// </summary>
public interface IIdempotencyService
{
    /// <summary>
    /// Checks if the given key has already been processed.
    /// </summary>
    Task<bool> ExistsAsync(IdempotencyKey key);

    /// <summary>
    /// Marks the key as being processed and persists the entry for transactional consistency.
    /// </summary>
    Task MarkAsProcessingAsync(IdempotencyKey key, string? typeName = null);

    /// <summary>
    /// Finalizes the key after a successful commit (e.g., upgrades cache state from processing to completed).
    /// </summary>
    Task MarkAsCompletedAsync(IdempotencyKey key);
}
