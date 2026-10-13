using System;

namespace Miqa.Platform.Messaging.Models;

/// <summary>
/// Represents a unique key for idempotency tracking across requests and events.
/// </summary>
public readonly record struct IdempotencyKey(Guid Id, IdempotencyScope Scope, string? Qualifier = null)
{
    public static IdempotencyKey ForRequest(Guid requestId) =>
        new(requestId, IdempotencyScope.Request);

    public static IdempotencyKey ForEvent(Guid eventId, string handlerName) =>
        new(eventId, IdempotencyScope.Event, handlerName);
}
