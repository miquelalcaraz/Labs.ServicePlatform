using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using QltS.Shared.Messaging.Idempotency;

namespace QltS.Shared.Persistence.EF.Relational.Configurations;


internal static class RequestIdempotencyConfiguration
{
    public static void ConfigureRequestIdempotency(this ModelBuilder builder)
    {
        builder.Entity<RequestIdempotency>().Configure();
    }

    public static void Configure(this EntityTypeBuilder<RequestIdempotency> builder)
    {
        builder.ToTable("RequestIdempotency", "infra");

        builder.HasKey(cr => cr.RequestId);

        builder.Property(cr => cr.RequestId)
            .IsRequired();

        builder.Property(cr => cr.CommandName)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(cr => cr.ProcessedAt)
            .IsRequired();

        // Create a unique index to enforce idempotency at the database level
        builder.HasIndex(cr => cr.RequestId)
            .IsUnique();
    }
}
