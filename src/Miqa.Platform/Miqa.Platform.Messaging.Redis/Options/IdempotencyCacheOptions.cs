using System;

namespace Miqa.Platform.Messaging.Options;

/// <summary>
/// Configuration options for the distributed idempotency cache.
/// </summary>
public sealed class IdempotencyCacheOptions
{
    public const string SectionName = "IdempotencyCache";

    public TimeSpan ProcessingTtl { get; set; } = TimeSpan.FromSeconds(30);

    public TimeSpan CompletedTtl { get; set; } = TimeSpan.FromMinutes(5);

    public string KeyPrefix { get; set; } = "idempotency:";
}
