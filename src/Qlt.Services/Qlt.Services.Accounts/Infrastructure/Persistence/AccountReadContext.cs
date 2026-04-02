using Microsoft.EntityFrameworkCore;

using Qlt.Platform.Persistence;
using Qlt.Platform.Persistence.Options;
using Qlt.Services.Accounts.Domain.Models.OrganizationAggregate;

namespace Qlt.Services.Accounts.Infrastructure.Persistence;

/// <summary>
/// Read-only DbContext for Accounts queries. Uses a read-only connection string when available.
/// NoTracking by default. Cannot persist changes.
/// </summary>
public class AccountReadContext : QltReadDbContext
{
    public DbSet<Organization> Organizations { get; set; } = null!;
    public DbSet<OrganizationContact> OrganizationContacts { get; set; } = null!;

    public AccountReadContext(
        DbContextOptions<AccountReadContext> options,
        QltReadDbContextConfiguration configuration)
        : base(options, configuration)
    {
    }
}
