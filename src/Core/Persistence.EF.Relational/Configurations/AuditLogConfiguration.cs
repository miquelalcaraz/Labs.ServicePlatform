using Microsoft.EntityFrameworkCore;

using QltS.Core.Domain.SeedWork;

namespace QltS.Core.Persistence.EF.Relational.Configurations;

internal static class AuditLogConfiguration
{
    private const string TableName = "AuditLogs";

    public static void UseAuditLogs(this ModelBuilder builder)
    {
        builder.Entity<AuditLog>(builder =>
        {
            builder.ToTable(TableName);
            builder.HasAlternateKey(x => x.AggregateId);
            builder.HasKey(x => x.Id);

            builder.Property(x => x.UserId);
            builder.Property(x => x.AggregateType).IsRequired();
            builder.Property(x => x.Timestamp).IsRequired();
            builder.Property(x => x.ActionType)
              .IsRequired()
              .HasConversion(
                   v => v.ToString(),
                   v => (AuditActionType)Enum.Parse(typeof(AuditActionType), v)
              );

            builder.Property(x => x.Event).IsRequired();
            builder.Property(x => x.EventType).IsRequired();

            builder.Ignore(x => x.Changes);
        });
    }
}