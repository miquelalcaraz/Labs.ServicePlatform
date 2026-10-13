using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Sirius.Services.Notifications.Domain.Models;

namespace Sirius.Services.Notifications.Infrastructure.Persistence.EntityConfigurations;

internal sealed class EmailNotificationConfiguration : IEntityTypeConfiguration<EmailNotification>
{
    private const string TableName = "EmailNotification";

    public void Configure(EntityTypeBuilder<EmailNotification> entity)
    {
        entity.ToTable(TableName);

        entity.HasKey(e => e.Id);

        entity.Property(e => e.Id)
            .ValueGeneratedNever();

        entity.Property(e => e.To)
            .IsRequired()
            .HasMaxLength(256);

        entity.Property(e => e.Subject)
            .IsRequired()
            .HasMaxLength(500);

        entity.Property(e => e.Body)
            .IsRequired();

        entity.Property(e => e.Type)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(50);

        entity.Property(e => e.Status)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(20);

        entity.Property(e => e.SentAtUtc);

        entity.Property(e => e.ErrorMessage)
            .HasMaxLength(2000);

        entity.Property(e => e.CorrelationId);
    }
}