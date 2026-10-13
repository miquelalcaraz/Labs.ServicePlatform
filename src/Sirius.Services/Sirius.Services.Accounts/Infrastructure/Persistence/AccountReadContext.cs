using Microsoft.EntityFrameworkCore;

using Sirius.Platform.Persistence;
using Sirius.Platform.Persistence.Options;
using Sirius.Services.Accounts.Domain.Models.CompanyAggregate;
using Sirius.Services.Accounts.Domain.Models.CustomerActivationProcessAggregate;
using Sirius.Services.Accounts.Domain.Models.OnboardingAggregate;
using Sirius.Services.Accounts.Domain.Models.SubscriptionAggregate;
using Sirius.Services.Accounts.Domain.Models.TenantAggregate;
using Sirius.Services.Accounts.Domain.Models.UserMembershipAggregate;

namespace Sirius.Services.Accounts.Infrastructure.Persistence;

/// <summary>
/// Read-only DbContext for Accounts queries. Uses a read-only connection string when available.
/// NoTracking by default. Cannot persist changes.
/// </summary>
public class AccountReadContext : SiriusReadDbContext
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
        SiriusReadDbContextConfiguration configuration)
        : base(options, configuration)
    {
    }
}
