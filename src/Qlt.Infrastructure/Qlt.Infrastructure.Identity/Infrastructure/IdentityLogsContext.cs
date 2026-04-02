using Microsoft.EntityFrameworkCore;

using Qlt.Platform.Persistence;
using Qlt.Platform.Persistence.Options;

namespace Qlt.Infrastructure.Identity.Infrastructure;

public class IdentityLogsContext : QltDBContext
{


    public IdentityLogsContext(DbContextOptions<IdentityLogsContext> options, QltDbContextConfiguration configuration)
            : base(options, configuration)
    {
        System.Diagnostics.Debug.WriteLine("IdentityContext::ctor ->" + this.GetHashCode());
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

    }


}

