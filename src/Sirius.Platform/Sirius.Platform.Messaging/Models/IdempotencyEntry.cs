using System;

namespace Sirius.Platform.Messaging.Models;

/// <summary>
/// Unified idempotency entry for both command requests and integration events.
/// </summary>
public class IdempotencyEntry
{
    public Guid Id { get; set; }

    public IdempotencyScope Scope { get; set; }

    public string Qualifier { get; set; } = string.Empty;

    public string? TypeName { get; set; }

    public DateTime ProcessedAt { get; set; }
}
