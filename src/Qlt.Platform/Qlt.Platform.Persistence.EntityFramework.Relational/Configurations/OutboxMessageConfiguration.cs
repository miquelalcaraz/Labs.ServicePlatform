using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Qlt.Platform.Messaging.Models;

namespace Qlt.Platform.Persistence.Configurations;


public static class OutboxMessageConfiguration
{
    public static void ConfigureTransactionalOutbox(this ModelBuilder builder)
    {
        builder.Entity<OutboxMessage>().Configure();
    }

    public static void Configure(this EntityTypeBuilder<OutboxMessage> entity)
    {
        entity.ToTable("OutboxMessage", "infra");

        entity.HasKey(e => e.EventId);

        entity.Property(e => e.EventTypeName)
            .IsRequired();

        entity.Property(e => e.Content)
            .IsRequired();

        entity.Property(e => e.CreationTime)
            .IsRequired();

        entity.Property(e => e.TransactionId)
            .IsRequired();

        entity.Property(e => e.RowVersion)
            .IsRowVersion()
            .IsRequired();

        entity.Ignore(e => e.IntegrationEvent);

        // Index for querying by transaction
        entity.HasIndex(e => e.TransactionId);

        // Index for querying pending events
        entity.HasIndex(e => new { e.State, e.CreationTime });
    }
}
