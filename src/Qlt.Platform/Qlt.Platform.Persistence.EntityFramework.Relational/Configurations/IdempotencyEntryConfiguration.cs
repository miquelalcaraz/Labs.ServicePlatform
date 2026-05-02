using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Qlt.Platform.Messaging.Models;

namespace Qlt.Platform.Persistence.Configurations;

internal static class IdempotencyEntryConfiguration
{
    public static void ConfigureIdempotencyEntry(this ModelBuilder builder)
    {
        builder.Entity<IdempotencyEntry>().Configure();
    }

    public static void Configure(this EntityTypeBuilder<IdempotencyEntry> entity)
    {
        entity.ToTable("IdempotencyEntry");

        entity.HasKey(e => new { e.Id, e.Scope, e.Qualifier });

        entity.Property(e => e.Scope)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(20);

        entity.Property(e => e.Qualifier)
            .IsRequired()
            .HasMaxLength(255)
            .HasDefaultValue(string.Empty);

        entity.Property(e => e.TypeName)
            .HasMaxLength(255);

        entity.Property(e => e.ProcessedAt)
            .IsRequired();

        entity.HasIndex(e => e.ProcessedAt);

        entity.HasIndex(e => new { e.Id, e.Scope });
    }
}
