using Core.Persistence.EF.Relational;

using MediatR;

using Microsoft.EntityFrameworkCore;

using QltSystem.Core.Persistence.EF.Extensions;
using QltSystem.Identity.Models;

namespace QltSystem.Identity.Infrastructure;

public class IdentityContext : QltDBContext
{
    public DbSet<Tenant> Tenants { get; set; } = null!;
    public DbSet<AdminUser> AdminUsers { get; set; } = null!;

    public IdentityContext(DbContextOptions<IdentityContext> options, IMediator mediator, QltDbContextConfiguration configuration)
            : base(options, mediator, configuration)
    {
        System.Diagnostics.Debug.WriteLine("IdentityContext::ctor ->" + this.GetHashCode());
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

    }


}

