using System;

namespace Qlt.Domain.SeedWork
{
    public enum AuditActionType
    {
        None,
        Create,
        Update,
        Delete
    }

    /// <summary>
    /// Placeholder POCO for audit log entries.
    /// Pending redesign: automatic capture via EF ChangeTracker + IUserHttpContext.
    /// See docs/backlog/audit-log-proposal.md for the design proposal.
    /// </summary>
    public class AuditLog
    {
        public Guid Id { get; set; }
        public Guid EntityId { get; set; }
        public string EntityType { get; set; } = null!;
        public string UserId { get; set; } = null!;
        public AuditActionType ActionType { get; set; }
        public DateTime Timestamp { get; set; }
        public string? Changes { get; set; }
    }

}
