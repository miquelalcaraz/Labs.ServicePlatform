using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using QltSystem.Identity.Models;


namespace QltSystem.Identity.Infrastructure.EntityConfigurations;

internal sealed class AdminUserConfiguration : IEntityTypeConfiguration<AdminUser>
{
    private const string TableName = "AdminUser";
    public void Configure(EntityTypeBuilder<AdminUser> entity)
    {
        entity.ToTable(TableName);

        entity.HasKey(e => e.Id);


        entity.Property(x => x.Id)
            .ValueGeneratedNever();

        entity.Property(u => u.Email)
            .IsRequired()
            .HasMaxLength(255);

        entity.Property(u => u.TemporaryPassword)
            .IsRequired()
            .HasMaxLength(100);

        entity.Property(u => u.TenantId)
            .IsRequired();

        entity.Property(u => u.CreatedAt)
            .IsRequired();

        // Index for quick tenant lookups
        entity.HasIndex(u => u.TenantId);

        // Unique email per tenant
        entity.HasIndex(u => new { u.TenantId, u.Email })
            .IsUnique();
    }

}