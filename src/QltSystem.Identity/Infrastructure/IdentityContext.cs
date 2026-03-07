using Microsoft.EntityFrameworkCore;

using QltSystem.Core.IntegrationEventLogEF;

namespace QltSystem.Identity.Infrastructure;

public class IdentityContext : DbContext
{

    public IdentityContext(DbContextOptions<IdentityContext> options) : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("identity");
        modelBuilder.UseEventIdempotency();
    }
}
