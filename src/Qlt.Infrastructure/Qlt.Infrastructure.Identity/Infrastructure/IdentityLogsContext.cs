using MediatR;

using Microsoft.EntityFrameworkCore;

using Qlt.Platform.Persistence;
using Qlt.Platform.Persistence.Extensions;

namespace Qlt.Infrastructure.Identity.Infrastructure;

public class IdentityLogsContext : QltDBContext
{


    public IdentityLogsContext(DbContextOptions<IdentityLogsContext> options, IMediator mediator, QltDbContextConfiguration configuration)
            : base(options, mediator, configuration)
    {
        System.Diagnostics.Debug.WriteLine("IdentityContext::ctor ->" + this.GetHashCode());
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

    }


}

