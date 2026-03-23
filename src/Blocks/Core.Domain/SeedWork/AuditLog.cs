using QltS.Shared.Messaging.DomainEvents;

using System;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;

namespace QltS.Core.Domain.SeedWork
{
    public enum AuditActionType
    {
        None,
        Create,
        Update,
        Delete,
        Block,
        UnBlock
    }

    public class AuditLog : Entity
    {
        public Guid AggregateId { get; private set; }
        public string AggregateType { get; private set; } = null!;
        public string UserId { get; private set; } = null!;
        public AuditActionType ActionType { get; private set; }
        public DateTime Timestamp { get; private set; }

        public string Event { get; set; } = null!;
        public string EventType { get; set; } = null!;

        [NotMapped]
        public IDomainEvent Changes
        {
            get
            {
                var type = Type.GetType(EventType);
                if (type == null)
                {
                    throw new InvalidOperationException(
                        $"Cannot load type '{EventType}'. " +
                        $"Ensure the assembly containing the type is loaded.");
                }

                var deserialized = JsonSerializer.Deserialize(Event, type);
                if (deserialized is not IDomainEvent domainEvent)
                {
                    throw new InvalidOperationException(
                        $"Deserialized event of type '{type.Name}' " +
                        $"does not implement IDomainEvent.");
                }

                return domainEvent;
            }
            private set
            {
                if (value == null)
                {
                    throw new ArgumentNullException(nameof(value),
                        "Domain event cannot be null");
                }

                Event = JsonSerializer.Serialize(value, value.GetType());
                EventType = value.GetType().AssemblyQualifiedName
                    ?? throw new InvalidOperationException(
                        $"Cannot get AssemblyQualifiedName for type '{value.GetType().Name}'");
            }
        }

        private AuditLog() { }

        public static AuditLog Create(Guid aggregateId, string aggregateType, string userId, AuditActionType actionType, IDomainEvent changes)
        {
            return new AuditLog
            {
                AggregateId = aggregateId,
                AggregateType = aggregateType,
                UserId = userId,
                ActionType = actionType,
                Timestamp = DateTime.UtcNow,
                Changes = changes
            };
        }
    }

}
