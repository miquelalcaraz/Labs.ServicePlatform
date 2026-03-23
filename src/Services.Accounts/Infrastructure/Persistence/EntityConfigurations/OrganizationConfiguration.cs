using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using QltS.Accounts.Domain.Models.OrganizationAggregate;

namespace QltS.Accounts.Infrastructure.Persistence.EntityConfigurations;

internal sealed class OrganizationConfiguration : IEntityTypeConfiguration<Organization>
{
    private const string TableName = "Organization";
    public void Configure(EntityTypeBuilder<Organization> entity)
    {
        entity.ToTable(TableName);
        entity.HasKey(e => e.Id);

        // ID is generated in code (Entity constructor generates GUID)
        // No database generation needed
        entity.Property(x => x.Id)
            .ValueGeneratedNever(); // ← Explicitly prevent DB generation

        entity.Property(e => e.TenantId)
            .IsRequired();
        entity.Property(e => e.OrganizationType)
             .HasConversion<string>()
            .IsRequired()
            .HasMaxLength(50);

        entity.Property(e => e.Name)
            .IsRequired()
            .HasMaxLength(200);

        entity.Property(e => e.LegalName)
            .IsRequired()
            .HasMaxLength(200);

        entity.OwnsOne(e => e.Address, address =>
        {
            address.Property(a => a.Street)
                .HasMaxLength(200);
            address.Property(a => a.City)
                .HasMaxLength(100);
            address.Property(a => a.State)
                .HasMaxLength(100);
            address.Property(a => a.PostalCode)
                .HasMaxLength(20);
            address.Property(a => a.Country)
                .HasMaxLength(100);
            address.Property(a => a.CountryCode)
                .HasMaxLength(10);
        });
        entity.Property(e => e.TaxNumber)
            .IsRequired()
            .HasMaxLength(50);

        entity.Property(e => e.TaxNumberType)
            .HasConversion<string>()
            .IsRequired();

        entity.Property(e => e.Language)
            .HasMaxLength(50);

        entity.Property(e => e.LanguageCode)
            .HasMaxLength(10);

        entity.Property(e => e.CurrencyCode)
            .HasMaxLength(10);
    }
}