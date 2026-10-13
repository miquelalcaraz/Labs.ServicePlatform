using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Miqa.Infrastructure.Identity.Models;


namespace Miqa.Infrastructure.Identity.Infrastructure.EntityConfigurations;

internal sealed class ApplicationUserConfiguration : IEntityTypeConfiguration<ApplicationUser>
{
    private const string TableName = "Users";
    public void Configure(EntityTypeBuilder<ApplicationUser> entity)
    {
        entity.ToTable(TableName);

        entity.HasKey(e => e.Id);


        entity.Property(x => x.Id)
            .ValueGeneratedNever();

        entity.Property(u => u.Email)
            .IsRequired()
            .HasMaxLength(255);



        entity.Property(u => u.TenantId)
            .IsRequired();

        entity.Property(u => u.CreatedAt)
            .IsRequired();

        // Index for quick tenant lookups
        entity.HasIndex(u => u.TenantId);

        // Unique email per tenant
        entity.HasIndex(u => new { u.TenantId, u.Email })
            .IsUnique();

        entity.HasOne(d => d.Tenant).WithMany(p => p.Users)
            .HasForeignKey(d => d.TenantId)
            .OnDelete(DeleteBehavior.ClientSetNull);
    }

}