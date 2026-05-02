using Microsoft.EntityFrameworkCore;

using Qlt.Platform.Persistence;
using Qlt.Platform.Persistence.Options;
using Qlt.Services.Accounts.Domain.Models.CompanyAggregate;
using Qlt.Services.Accounts.Domain.Models.CustomerActivationProcessAggregate;
using Qlt.Services.Accounts.Domain.Models.OnboardingAggregate;
using Qlt.Services.Accounts.Domain.Models.SubscriptionAggregate;
using Qlt.Services.Accounts.Domain.Models.TenantAggregate;
using Qlt.Services.Accounts.Domain.Models.UserMembershipAggregate;

namespace Qlt.Services.Accounts.Infrastructure.Persistence;

/// <summary>
/// Read-only DbContext for Accounts queries. Uses a read-only connection string when available.
/// NoTracking by default. Cannot persist changes.
/// </summary>
public class AccountReadContext : QltReadDbContext
{
    public DbSet<Company> Companies { get; set; } = null!;
    public DbSet<CompanyContact> CompanyContacts { get; set; } = null!;
    public DbSet<Tenant> Tenants { get; set; } = null!;
    public DbSet<Subscription> Subscriptions { get; set; } = null!;
    public DbSet<UserMembership> UserMemberships { get; set; } = null!;
    public DbSet<OnboardingProcess> OnboardingProcesses { get; set; } = null!;
    public DbSet<CustomerActivationProcess> CustomerActivationProcesses { get; set; } = null!;

    public AccountReadContext(
        DbContextOptions<AccountReadContext> options,
        QltReadDbContextConfiguration configuration)
        : base(options, configuration)
    {
    }
}
