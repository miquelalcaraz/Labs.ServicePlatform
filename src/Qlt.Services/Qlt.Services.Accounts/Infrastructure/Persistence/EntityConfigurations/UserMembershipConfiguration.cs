using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Qlt.Services.Accounts.Domain.Models.UserMembershipAggregate;

namespace Qlt.Services.Accounts.Infrastructure.Persistence.EntityConfigurations;

internal sealed class UserMembershipConfiguration : IEntityTypeConfiguration<UserMembership>
{
    private const string TableName = "UserMembership";

    public void Configure(EntityTypeBuilder<UserMembership> entity)
    {
        entity.ToTable(TableName);
        entity.HasKey(e => e.Id);

        entity.Property(x => x.Id)
            .ValueGeneratedNever();

        entity.Property(e => e.UserId)
            .IsRequired();

        entity.Property(e => e.TenantId)
            .IsRequired();

        entity.Property(e => e.CompanyId)
            .IsRequired();

        entity.Property(e => e.MembershipType)
            .HasConversion<string>()
            .IsRequired()
            .HasMaxLength(50);

        entity.Property(e => e.RoleCode)
            .HasConversion<string>()
            .IsRequired()
            .HasMaxLength(50);

        entity.Property(e => e.Status)
            .HasConversion<string>()
            .IsRequired()
            .HasMaxLength(50);

        entity.Property(e => e.IsPrimary)
            .IsRequired();

        entity.Property(e => e.CreatedByInternalUserId);

        entity.Property(e => e.ActivatedAtUtc);

        entity.Property(e => e.RevokedAtUtc);

        entity.HasIndex(e => e.UserId);
        entity.HasIndex(e => e.TenantId);
        entity.HasIndex(e => e.CompanyId);
    }
}
