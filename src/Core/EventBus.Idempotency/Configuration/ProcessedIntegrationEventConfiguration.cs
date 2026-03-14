using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using QltSystem.Core.EventBus.Idempotency.Models;

namespace QltSystem.Core.EventBus.Idempotency.Configuration;

/// <summary>
/// Entity Framework configuration for ProcessedIntegrationEvent.
/// Configures the table schema and constraints for event idempotency tracking.
/// This configuration is automatically applied when the EventBus.Idempotency assembly 
/// is registered with ApplyConfigurationsFromAssembly.
/// </summary>
public class ProcessedIntegrationEventConfiguration : IEntityTypeConfiguration<ProcessedIntegrationEvent>
{
    public void Configure(EntityTypeBuilder<ProcessedIntegrationEvent> entity)
    {
        entity.ToTable("ProcessedIntegrationEvents");

        // Composite primary key: same event can be processed by different handlers
        entity.HasKey(e => new { e.EventId, e.HandlerName });

        entity.Property(e => e.HandlerName)
            .HasMaxLength(255)
            .IsRequired();

        entity.Property(e => e.ProcessedAt)
            .IsRequired();

        entity.Property(e => e.EventType)
            .HasMaxLength(255);

        // Index for querying by event ID
        entity.HasIndex(e => e.EventId);

        // Index for querying by processed date (useful for cleanup jobs)
        entity.HasIndex(e => e.ProcessedAt);
    }
}
