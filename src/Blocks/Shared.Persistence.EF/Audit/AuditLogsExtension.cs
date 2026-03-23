
using Microsoft.EntityFrameworkCore;

using QltS.Core.Domain.SeedWork;


namespace QltS.Shared.Persistence.EF.Audit;

public static class AuditLogsExtension
{
    public static async Task SaveAuditLogsAsync(this DbContext ctx, CancellationToken cancellationToken)
    {
        var domainEntities = ctx.ChangeTracker
           .Entries<Entity>()
           .Where(x => x.Entity.AuditLogs != null && x.Entity.AuditLogs.Any());

        var auditLogs = domainEntities.SelectMany(entity =>
        {
            var logs = entity.Entity.AuditLogs;
            entity.Entity.RemoveAudits();
            return logs;
        }).ToList();


        if (auditLogs is not null && auditLogs.Any())
        {
            await ctx.Set<AuditLog>().AddRangeAsync(auditLogs, cancellationToken);
        }
    }
}

