using System;

using Qlt.Platform.Messaging.IntegrationEvents;

namespace Qlt.Platform.Messaging.Models;

/// <summary>
/// Represents an integration event stored in the transactional outbox.
/// Ensures reliable event publishing using the Transactional Outbox pattern.
/// </summary>
public class OutboxMessage
{
    public Guid Id { get; set; }

    public string TypeName { get; set; } = string.Empty;

    public OutboxMessageStatus Status { get; set; }

    public int SendCount { get; set; }

    public DateTime CreatedAt { get; set; }

    public string Payload { get; set; } = string.Empty;

    public Guid TransactionId { get; set; }

    public byte[] RowVersion { get; set; } = null!;

    /// <summary>
    /// Transient property populated after deserialization of <see cref="Payload"/>. Not persisted.
    /// </summary>
    public IntegrationEvent? DeserializedEvent { get; set; }
}
