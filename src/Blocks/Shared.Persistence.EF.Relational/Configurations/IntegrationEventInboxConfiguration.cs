using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using QltS.Shared.Messaging.Inbox;

namespace QltS.Shared.Persistence.EF.Relational.Configurations;


internal static class IntegrationEventInboxConfiguration
{
    public static void ConfigureIntegrationEventInbox(this ModelBuilder builder)
    {
        builder.Entity<IntegrationEventInbox>().Configure();
    }
    public static void Configure(this EntityTypeBuilder<IntegrationEventInbox> entity)
    {
        entity.ToTable("IntegrationEventInbox", "infra");

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
