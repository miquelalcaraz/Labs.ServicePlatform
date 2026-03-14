using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using QltSystem.Core.EventBus.TransactionalOutbox.Models;

namespace QltSystem.Core.EventBus.TransactionalOutbox.Configuration;

/// <summary>
/// Entity Framework configuration for IntegrationEventLogEntry.
/// Configures the table schema for the transactional outbox pattern.
/// This configuration is automatically applied when the EventBus.TransactionalOutbox assembly 
/// is registered with ApplyConfigurationsFromAssembly.
/// </summary>
public class IntegrationEventLogConfiguration : IEntityTypeConfiguration<IntegrationEventLogEntry>
{
    public void Configure(EntityTypeBuilder<IntegrationEventLogEntry> entity)
    {
        entity.ToTable("IntegrationEventLog");

        entity.HasKey(e => e.EventId);

        entity.Property(e => e.EventTypeName)
            .IsRequired();

        entity.Property(e => e.Content)
            .IsRequired();

        entity.Property(e => e.CreationTime)
            .IsRequired();

        entity.Property(e => e.TransactionId)
            .IsRequired();

        // Index for querying by transaction
        entity.HasIndex(e => e.TransactionId);

        // Index for querying pending events
        entity.HasIndex(e => new { e.State, e.CreationTime });
    }
}
