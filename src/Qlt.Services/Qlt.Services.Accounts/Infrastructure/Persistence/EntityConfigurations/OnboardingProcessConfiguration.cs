using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Qlt.Services.Accounts.Domain.Models.OnboardingAggregate;

namespace Qlt.Services.Accounts.Infrastructure.Persistence.EntityConfigurations;

internal sealed class OnboardingProcessConfiguration : IEntityTypeConfiguration<OnboardingProcess>
{
    private const string TableName = "OnboardingProcess";

    public void Configure(EntityTypeBuilder<OnboardingProcess> entity)
    {
        entity.ToTable(TableName);
        entity.HasKey(e => e.Id);

        entity.Property(x => x.Id)
            .ValueGeneratedNever();

        entity.Property(e => e.SubscriptionId)
            .IsRequired();

        entity.Property(e => e.TenantId)
            .IsRequired();

        entity.Property(e => e.OwnerCompanyId);

        entity.Property(e => e.AdminUserId);

        entity.Property(e => e.Status)
            .HasConversion<string>()
            .IsRequired()
            .HasMaxLength(50);

        entity.Property(e => e.CurrentStep)
            .HasConversion<string>()
            .IsRequired()
            .HasMaxLength(50);

        entity.Property(e => e.ErrorCode)
            .HasMaxLength(100);

        entity.Property(e => e.ErrorMessage)
            .HasMaxLength(1000);

        entity.Property(e => e.StartedAtUtc)
            .IsRequired();

        entity.Property(e => e.CompletedAtUtc);

        entity.Property(e => e.FailedAtUtc);

        entity.Property(e => e.InitiatedByInternalUserId)
            .IsRequired();

        entity.Property(e => e.CorrelationId);

        entity.Property(e => e.RetryCount)
            .IsRequired();

        entity.Property(e => e.LastRetryAtUtc);

        entity.Property(e => e.FailedStep)
            .HasConversion<string>()
            .HasMaxLength(50);

        entity.Property(e => e.CompanyLegalName)
            .HasMaxLength(250);

        entity.Property(e => e.SubscriptionPlanCode)
            .HasMaxLength(100);

        entity.Property(e => e.AdminEmail)
            .HasMaxLength(320);

        entity.Property(e => e.AdminFirstName)
            .HasMaxLength(100);

        entity.Property(e => e.AdminLastName)
            .HasMaxLength(150);

        entity.Property(e => e.TenantName)
            .HasMaxLength(250);

        entity.Property(e => e.AdminMembershipId);

        entity.HasIndex(e => e.SubscriptionId)
            .IsUnique();

        entity.HasIndex(e => e.TenantId);

        entity.HasIndex(e => e.CorrelationId);
    }
}
