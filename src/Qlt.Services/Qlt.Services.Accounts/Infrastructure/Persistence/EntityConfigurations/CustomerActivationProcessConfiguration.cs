using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Qlt.Services.Accounts.Domain.Models.CustomerActivationProcessAggregate;

namespace Qlt.Services.Accounts.Infrastructure.Persistence.EntityConfigurations;

internal sealed class CustomerActivationProcessConfiguration : IEntityTypeConfiguration<CustomerActivationProcess>
{
    private const string TableName = "CustomerActivationProcess";

    public void Configure(EntityTypeBuilder<CustomerActivationProcess> entity)
    {
        entity.ToTable(TableName);
        entity.HasKey(e => e.Id);

        entity.Property(x => x.Id)
            .ValueGeneratedNever();

        entity.Property(e => e.SubscriptionId)
            .IsRequired();

        entity.Property(e => e.TenantId)
            .IsRequired();

        entity.Property(e => e.OwnerCompanyId)
            .IsRequired();

        entity.Property(e => e.AdminUserId)
            .IsRequired();

        entity.Property(e => e.AdminMembershipId)
            .IsRequired();

        entity.Property(e => e.Status)
            .HasConversion<string>()
            .IsRequired()
            .HasMaxLength(50);

        entity.Property(e => e.CurrentStep)
            .HasConversion<string>()
            .IsRequired()
            .HasMaxLength(50);

        entity.Property(e => e.RetryCount)
            .IsRequired();

        entity.Property(e => e.LastErrorCode)
            .HasMaxLength(100);

        entity.Property(e => e.LastErrorMessage)
            .HasMaxLength(1000);

        entity.Property(e => e.StartedAtUtc)
            .IsRequired();

        entity.Property(e => e.CompletedAtUtc);

        entity.Property(e => e.ActivationRequestedAtUtc);

        entity.Property(e => e.ActivationDeliveredAtUtc);

        entity.Property(e => e.UserActivatedAtUtc);

        entity.Property(e => e.LastRetryAtUtc);

        entity.Property(e => e.CompanyLegalName)
            .HasMaxLength(500);

        entity.Property(e => e.SubscriptionPlanCode)
            .HasMaxLength(100);

        entity.Property(e => e.AdminEmail)
            .HasMaxLength(256);

        entity.Property(e => e.CorrelationId);

        entity.HasIndex(e => e.SubscriptionId)
            .IsUnique();

        entity.HasIndex(e => e.TenantId);

        entity.HasIndex(e => e.CorrelationId);
    }
}
