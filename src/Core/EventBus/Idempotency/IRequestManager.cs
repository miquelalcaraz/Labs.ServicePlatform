using System;
using System.Threading.Tasks;

namespace QltSystem.Core.EventBus.Idempotency;

/// <summary>
/// Manages command request idempotency by tracking processed requests.
/// Ensures that duplicate commands with the same request ID are not processed multiple times.
/// </summary>
public interface IRequestManager
{
    /// <summary>
    /// Checks if a request with the specified ID has already been processed.
    /// </summary>
    /// <param name="id">The unique identifier of the request</param>
    /// <returns>True if the request exists, false otherwise</returns>
    Task<bool> ExistAsync(Guid id);

    /// <summary>
    /// Marks a request as processed by storing it with the command type information.
    /// </summary>
    /// <typeparam name="T">The type of command being processed</typeparam>
    /// <param name="id">The unique identifier of the request</param>
    /// <returns>A task representing the asynchronous operation</returns>
    Task CreateRequestForCommandAsync<T>(Guid id);
}
