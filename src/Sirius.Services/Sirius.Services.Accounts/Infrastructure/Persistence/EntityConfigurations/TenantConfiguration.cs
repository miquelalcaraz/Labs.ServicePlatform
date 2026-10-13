using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Sirius.Services.Accounts.Domain.Models.TenantAggregate;

namespace Sirius.Services.Accounts.Infrastructure.Persistence.EntityConfigurations;

internal sealed class TenantConfiguration : IEntityTypeConfiguration<Tenant>
{
    private const string TableName = "Tenant";

    public void Configure(EntityTypeBuilder<Tenant> entity)
    {
        entity.ToTable(TableName);
        entity.HasKey(e => e.Id);

        entity.Property(x => x.Id)
            .ValueGeneratedNever();

        entity.Property(e => e.Name)
            .IsRequired()
            .HasMaxLength(200);

        entity.Property(e => e.Status)
            .HasConversion<string>()
            .IsRequired()
            .HasMaxLength(50);

        entity.Property(e => e.OwnerCompanyId);

        entity.Property(e => e.CreatedByInternalUserId)
            .IsRequired();

        entity.Property(e => e.ActivatedAtUtc);

        entity.Property(e => e.ClosedAtUtc);

        entity.HasIndex(e => e.OwnerCompanyId);
    }
}
