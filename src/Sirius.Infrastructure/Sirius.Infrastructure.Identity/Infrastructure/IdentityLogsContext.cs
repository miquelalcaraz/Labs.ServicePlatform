using Microsoft.EntityFrameworkCore;

using Sirius.Platform.Persistence;
using Sirius.Platform.Persistence.Options;

namespace Sirius.Infrastructure.Identity.Infrastructure;

public class IdentityLogsContext : SiriusDBContext
{


    public IdentityLogsContext(DbContextOptions<IdentityLogsContext> options, SiriusDbContextConfiguration configuration)
            : base(options, configuration)
    {
        System.Diagnostics.Debug.WriteLine("IdentityContext::ctor ->" + this.GetHashCode());
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

    }


}

