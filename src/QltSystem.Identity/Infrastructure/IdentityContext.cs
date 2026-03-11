using Core.Persistence.EF.Relational.Configurations;

using Microsoft.EntityFrameworkCore;

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
