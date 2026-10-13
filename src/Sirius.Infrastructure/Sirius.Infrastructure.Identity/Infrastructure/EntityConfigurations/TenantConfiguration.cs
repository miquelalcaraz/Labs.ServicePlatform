using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Sirius.Infrastructure.Identity.Models;

namespace Sirius.Infrastructure.Identity.Infrastructure.EntityConfigurations;

internal sealed class TenantConfiguration : IEntityTypeConfiguration<Tenant>
{
    private const string TableName = "Tenant";
    public void Configure(EntityTypeBuilder<Tenant> entity)
    {
        entity.ToTable(TableName);
        entity.HasKey(e => e.Id);


        entity.Property(x => x.Id)
            .ValueGeneratedNever();

        entity.Property(t => t.Name)
            .IsRequired()
            .HasMaxLength(200);

        entity.Property(t => t.CompanyId)
            .IsRequired();

        entity.HasIndex(t => t.CompanyId)
            .IsUnique();

        entity.Property(t => t.CreatedAt)
            .IsRequired();
    }
}