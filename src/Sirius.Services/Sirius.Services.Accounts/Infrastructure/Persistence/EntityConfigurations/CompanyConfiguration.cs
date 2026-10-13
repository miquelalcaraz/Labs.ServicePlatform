using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Sirius.Services.Accounts.Domain.Models.CompanyAggregate;

namespace Sirius.Services.Accounts.Infrastructure.Persistence.EntityConfigurations;

internal sealed class CompanyConfiguration : IEntityTypeConfiguration<Company>
{
    private const string TableName = "Company";

    public void Configure(EntityTypeBuilder<Company> entity)
    {
        entity.ToTable(TableName);
        entity.HasKey(e => e.Id);

        entity.Property(x => x.Id)
            .ValueGeneratedNever();

        entity.Property(e => e.TenantId)
            .IsRequired();

        entity.Property(e => e.LegalName)
            .IsRequired()
            .HasMaxLength(200);

        entity.Property(e => e.TradeName)
            .IsRequired()
            .HasMaxLength(200);

        entity.Property(e => e.CompanyType)
            .HasConversion<string>()
            .IsRequired()
            .HasMaxLength(50);

        entity.Property(e => e.Status)
            .HasConversion<string>()
            .IsRequired()
            .HasMaxLength(50);

        entity.OwnsOne(e => e.Address, address =>
        {
            address.Property(a => a.Street).HasMaxLength(200);
            address.Property(a => a.City).HasMaxLength(100);
            address.Property(a => a.State).HasMaxLength(100);
            address.Property(a => a.PostalCode).HasMaxLength(20);
            address.Property(a => a.Country).HasMaxLength(100);
            address.Property(a => a.CountryCode).HasMaxLength(10);
        });

        entity.Property(e => e.TaxId)
            .IsRequired()
            .HasMaxLength(50);

        entity.Property(e => e.TaxNumberType)
            .HasConversion<string>()
            .IsRequired();

        entity.Property(e => e.CreatedByInternalUserId);

        entity.HasIndex(e => e.TenantId);
    }
}
