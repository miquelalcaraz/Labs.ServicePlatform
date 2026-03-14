using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using QltSystem.Core.Persistence.EF.Idempotency;

namespace Core.Persistence.EF.Relational.Configurations;

/// <summary>
/// Entity Framework configuration for ClientRequest entity.
/// Configures the table mapping and ensures unique constraint on request IDs for command idempotency.
/// This configuration is specific to relational databases.
/// </summary>
public class ClientRequestConfiguration : IEntityTypeConfiguration<ClientRequest>
{
    public void Configure(EntityTypeBuilder<ClientRequest> builder)
    {
        builder.ToTable("ClientRequests");

        builder.HasKey(cr => cr.Id);

        builder.Property(cr => cr.Id)
            .IsRequired();

        builder.Property(cr => cr.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(cr => cr.Time)
            .IsRequired();

        // Create a unique index to enforce idempotency at the database level
        builder.HasIndex(cr => cr.Id)
            .IsUnique();
    }
}
