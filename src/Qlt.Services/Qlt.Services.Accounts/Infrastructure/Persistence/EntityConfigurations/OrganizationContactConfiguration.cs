using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Qlt.Services.Accounts.Domain.Models.OrganizationAggregate;

namespace Qlt.Services.Accounts.Infrastructure.Persistence.EntityConfigurations;

internal sealed class OrganizationContactConfiguration : IEntityTypeConfiguration<OrganizationContact>
{
    private const string TableName = "OrganizationContact";
    public void Configure(EntityTypeBuilder<OrganizationContact> entity)
    {
        entity.ToTable(TableName);

        entity.HasKey(e => e.Id);

        // ID is generated in code (Entity constructor generates GUID)
        // No database generation needed
        entity.Property(x => x.Id)
            .ValueGeneratedNever(); // ← Explicitly prevent DB generation

        entity.Property(e => e.OrganizationId)
            .IsRequired();

        entity.Property(e => e.FirstName)
            .IsRequired()
            .HasMaxLength(100);

        entity.Property(e => e.LastName)
            .IsRequired()
            .HasMaxLength(100);

        entity.Property(e => e.Email)
            .IsRequired()
            .HasMaxLength(256);

        entity.Property(e => e.PhoneNumber)
            .HasMaxLength(20);

        entity.Property(e => e.JobTitle)
            .HasMaxLength(100);

        entity.HasIndex(e => e.OrganizationId);

        entity.HasIndex(e => e.Email);
        entity.HasOne(d => d.Organization).WithMany(p => p.OrganizationContacts)
            .HasForeignKey(d => d.OrganizationId)
            .HasConstraintName("FK_Organization_OrganizationContacts");
    }

}