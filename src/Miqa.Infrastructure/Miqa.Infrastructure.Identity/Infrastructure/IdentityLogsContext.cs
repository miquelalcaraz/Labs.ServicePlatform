using Microsoft.EntityFrameworkCore;

using Miqa.Platform.Persistence;
using Miqa.Platform.Persistence.Options;

namespace Miqa.Infrastructure.Identity.Infrastructure;

public class IdentityLogsContext : MiqaDBContext
{


    public IdentityLogsContext(DbContextOptions<IdentityLogsContext> options, MiqaDbContextConfiguration configuration)
            : base(options, configuration)
    {
        System.Diagnostics.Debug.WriteLine("IdentityContext::ctor ->" + this.GetHashCode());
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

    }


}

