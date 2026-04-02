using System;

namespace Qlt.Platform.Messaging.Commands;

/// <summary>
/// Non-generic base interface for idempotent requests, enabling simple pattern matching
/// without reflection in pipeline behaviors.
/// </summary>
public interface IIdempotentRequest
{
    Guid RequestId { get; }

    object CreateDuplicateResult();
}
