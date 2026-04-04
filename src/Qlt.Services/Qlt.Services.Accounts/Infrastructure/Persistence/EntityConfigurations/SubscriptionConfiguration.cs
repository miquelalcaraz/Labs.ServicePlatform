using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Qlt.Services.Accounts.Domain.Models.SubscriptionAggregate;

namespace Qlt.Services.Accounts.Infrastructure.Persistence.EntityConfigurations;

internal sealed class SubscriptionConfiguration : IEntityTypeConfiguration<Subscription>
{
    private const string TableName = "Subscription";

    public void Configure(EntityTypeBuilder<Subscription> entity)
    {
        entity.ToTable(TableName);
        entity.HasKey(e => e.Id);

        entity.Property(x => x.Id)
            .ValueGeneratedNever();

        entity.Property(e => e.TenantId)
            .IsRequired();

        entity.Property(e => e.OwnerCompanyId);

        entity.Property(e => e.PlanCode)
            .IsRequired()
            .HasMaxLength(100);

        entity.Property(e => e.Status)
            .HasConversion<string>()
            .IsRequired()
            .HasMaxLength(50);

        entity.Property(e => e.StartDateUtc);

        entity.Property(e => e.EndDateUtc);

        entity.Property(e => e.CreatedByInternalUserId)
            .IsRequired();

        entity.Property(e => e.ActivatedAtUtc);

        entity.Property(e => e.SuspendedAtUtc);

        entity.Property(e => e.CancelledAtUtc);

        entity.HasIndex(e => e.TenantId);
    }
}
