using Microsoft.EntityFrameworkCore;

using Sirius.Domain.SeedWork;

namespace Sirius.Platform.Persistence.Configurations;

internal static class AuditLogConfiguration
{
    private const string TableName = "Logs";

    public static void ConfigureAuditLogs(this ModelBuilder builder)
    {
        builder.Entity<AuditLog>(entity =>
        {
            entity.ToTable(TableName, "audit");
            entity.HasKey(x => x.Id);

            entity.Property(x => x.EntityId).IsRequired();
            entity.Property(x => x.EntityType).IsRequired();
            entity.Property(x => x.UserId).IsRequired();
            entity.Property(x => x.Timestamp).IsRequired();
            entity.Property(x => x.ActionType)
                .IsRequired()
                .HasConversion(
                    v => v.ToString(),
                    v => (AuditActionType)Enum.Parse(typeof(AuditActionType), v)
                );

            entity.Property(x => x.Changes);

            entity.HasIndex(x => x.EntityId);
            entity.HasIndex(x => new { x.EntityType, x.Timestamp });
        });
    }
}