using System;
using System.Text.Json;

using Qlt.Platform.Messaging.IntegrationEvents;

namespace Qlt.Platform.Messaging.Models;

/// <summary>
/// Represents an integration event stored in the transactional outbox.
/// Ensures reliable event publishing using the Transactional Outbox pattern.
/// </summary>
public class OutboxMessage
{
    private static readonly JsonSerializerOptions s_indentedOptions = new() { WriteIndented = true };
    private static readonly JsonSerializerOptions s_caseInsensitiveOptions = new() { PropertyNameCaseInsensitive = true };

    private OutboxMessage() { }

    public OutboxMessage(IntegrationEvent @event, Guid transactionId)
    {
        EventId = @event.Id;
        CreationTime = @event.CreationDate;
        EventTypeName = @event.GetType().FullName;
        Content = JsonSerializer.Serialize(@event, @event.GetType(), s_indentedOptions);
        State = EventStateEnum.NotPublished;
        TimesSent = 0;
        TransactionId = transactionId;
    }

    public Guid EventId { get; private set; }

    public string EventTypeName { get; private set; } = null!;

    public IntegrationEvent? IntegrationEvent { get; private set; }

    public EventStateEnum State { get; set; }

    public int TimesSent { get; set; }

    public DateTime CreationTime { get; private set; }

    public string Content { get; private set; } = null!;

    public Guid TransactionId { get; private set; }

    public byte[] RowVersion { get; private set; } = null!;

    public OutboxMessage DeserializeJsonContent(Type type)
    {
        if (type == null)
        {
            throw new ArgumentNullException(nameof(type),
                $"Cannot find event type for '{EventTypeName}'. Ensure the event type exists in the entry assembly.");
        }

        IntegrationEvent = JsonSerializer.Deserialize(Content, type, s_caseInsensitiveOptions) as IntegrationEvent;
        return this;
    }
}
