using MediatR;

using Microsoft.EntityFrameworkCore;

using QltS.Shared.Persistence.EF.Extensions;
using QltS.Shared.Persistence.EF.Relational;

namespace QltS.Identity.Infrastructure;

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

