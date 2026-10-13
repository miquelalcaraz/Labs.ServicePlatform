using Microsoft.EntityFrameworkCore;

using Miqa.Platform.Persistence;
using Miqa.Platform.Persistence.Options;
using Miqa.Services.Accounts.Domain.Models.CompanyAggregate;
using Miqa.Services.Accounts.Domain.Models.CustomerActivationProcessAggregate;
using Miqa.Services.Accounts.Domain.Models.OnboardingAggregate;
using Miqa.Services.Accounts.Domain.Models.SubscriptionAggregate;
using Miqa.Services.Accounts.Domain.Models.TenantAggregate;
using Miqa.Services.Accounts.Domain.Models.UserMembershipAggregate;

namespace Miqa.Services.Accounts.Infrastructure.Persistence;

/// <summary>
/// Read-only DbContext for Accounts queries. Uses a read-only connection string when available.
/// NoTracking by default. Cannot persist changes.
/// </summary>
public class AccountReadContext : MiqaReadDbContext
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
        MiqaReadDbContextConfiguration configuration)
        : base(options, configuration)
    {
    }
}
