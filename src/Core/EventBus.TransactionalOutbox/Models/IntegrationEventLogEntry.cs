using QltSystem.Core.EventBus.Contracts.Events;

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;

namespace QltSystem.Core.EventBus.TransactionalOutbox.Models;

/// <summary>
/// Represents an integration event stored in the transactional outbox.
/// Ensures reliable event publishing using the Transactional Outbox pattern.
/// </summary>
public class IntegrationEventLogEntry
{
    private static readonly JsonSerializerOptions s_indentedOptions = new() { WriteIndented = true };
    private static readonly JsonSerializerOptions s_caseInsensitiveOptions = new() { PropertyNameCaseInsensitive = true };

    private IntegrationEventLogEntry() { }

    public IntegrationEventLogEntry(IntegrationEvent @event, Guid transactionId)
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

    [Required]
    public string EventTypeName { get; private set; } = null!;

    [NotMapped]
    public IntegrationEvent? IntegrationEvent { get; private set; }

    public EventStateEnum State { get; set; }

    public int TimesSent { get; set; }

    public DateTime CreationTime { get; private set; }

    [Required]
    public string Content { get; private set; } = null!;

    public Guid TransactionId { get; private set; }

    public IntegrationEventLogEntry DeserializeJsonContent(Type type)
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
