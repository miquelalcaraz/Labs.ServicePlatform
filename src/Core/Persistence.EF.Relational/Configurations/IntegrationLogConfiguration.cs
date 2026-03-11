using Microsoft.EntityFrameworkCore;

using QltSystem.Core.Persistence.EF.IntegrationEventLog;

namespace Core.Persistence.EF.Relational.Configurations;

public static class IntegrationLogConfiguration
{
    public static void UseIntegrationEventLogs(this ModelBuilder builder)
    {
        builder.Entity<IntegrationEventLogEntry>(builder =>
        {
            builder.ToTable("IntegrationEventLog");

            builder.HasKey(e => e.EventId);
        });
    }


}
