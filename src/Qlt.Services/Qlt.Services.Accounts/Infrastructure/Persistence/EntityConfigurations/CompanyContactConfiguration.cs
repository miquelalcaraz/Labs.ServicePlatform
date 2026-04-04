using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Qlt.Services.Accounts.Domain.Models.CompanyAggregate;

namespace Qlt.Services.Accounts.Infrastructure.Persistence.EntityConfigurations;

internal sealed class CompanyContactConfiguration : IEntityTypeConfiguration<CompanyContact>
{
    private const string TableName = "CompanyContact";

    public void Configure(EntityTypeBuilder<CompanyContact> entity)
    {
        entity.ToTable(TableName);

        entity.HasKey(e => e.Id);

        entity.Property(x => x.Id)
            .ValueGeneratedNever();

        entity.Property(e => e.CompanyId)
            .IsRequired();

        entity.Property(e => e.FirstName)
            .IsRequired()
            .HasMaxLength(100);

        entity.Property(e => e.LastName)
            .HasMaxLength(100);

        entity.Property(e => e.Email)
            .IsRequired()
            .HasMaxLength(256);

        entity.Property(e => e.PhoneNumber)
            .HasMaxLength(20);

        entity.Property(e => e.JobTitle)
            .HasMaxLength(100);

        entity.HasIndex(e => e.CompanyId);

        entity.HasIndex(e => e.Email);

        entity.HasOne(d => d.Company)
            .WithMany(p => p.Contacts)
            .HasForeignKey(d => d.CompanyId)
            .HasConstraintName("FK_Company_CompanyContacts");
    }
}
