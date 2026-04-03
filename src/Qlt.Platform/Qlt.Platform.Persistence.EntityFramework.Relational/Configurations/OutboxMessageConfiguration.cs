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

        entity.HasKey(e => e.Id);

        entity.Property(e => e.TypeName)
            .IsRequired();

        entity.Property(e => e.Payload)
            .IsRequired();

        entity.Property(e => e.CreatedAt)
            .IsRequired();

        entity.Property(e => e.TransactionId)
            .IsRequired();

        entity.Property(e => e.RowVersion)
            .IsRowVersion()
            .IsRequired();

        entity.HasIndex(e => e.TransactionId);

        entity.HasIndex(e => new { e.Status, e.CreatedAt });

        entity.Ignore(e => e.DeserializedEvent);
    }
}
