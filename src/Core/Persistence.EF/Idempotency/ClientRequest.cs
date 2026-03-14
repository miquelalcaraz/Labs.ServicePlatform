using System;
using System.ComponentModel.DataAnnotations;

namespace QltSystem.Core.Persistence.EF.Idempotency;

/// <summary>
/// Represents a processed command request for idempotency purposes.
/// Tracks which commands have been executed to prevent duplicate processing.
/// </summary>
public class ClientRequest
{
    /// <summary>
    /// Unique identifier of the request (typically a correlation/request ID from the client).
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Name of the command type that was processed.
    /// </summary>
    [Required]
    public string Name { get; set; } = null!;

    /// <summary>
    /// Timestamp when the request was first processed.
    /// </summary>
    public DateTime Time { get; set; }
}
