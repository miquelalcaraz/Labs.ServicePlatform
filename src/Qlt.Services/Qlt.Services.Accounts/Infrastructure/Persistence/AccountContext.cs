using MediatR;

using Microsoft.EntityFrameworkCore;

using Qlt.Platform.Persistence;
using Qlt.Platform.Persistence.Options;
using Qlt.Services.Accounts.Domain.Models.CompanyAggregate;
using Qlt.Services.Accounts.Domain.Models.CustomerActivationProcessAggregate;
using Qlt.Services.Accounts.Domain.Models.OnboardingAggregate;
using Qlt.Services.Accounts.Domain.Models.SubscriptionAggregate;
using Qlt.Services.Accounts.Domain.Models.TenantAggregate;
using Qlt.Services.Accounts.Domain.Models.UserMembershipAggregate;

namespace Qlt.Services.Accounts.Infrastructure.Persistence
{
    /// <summary>
    /// Database context for Accounts bounded context.
    /// Manages Company, Tenant, Subscription, UserMembership, and OnboardingProcess aggregates.
    /// </summary>
    public class AccountContext : QltDBContext
    {
        public DbSet<Company> Companies { get; set; } = null!;

        public DbSet<CompanyContact> CompanyContacts { get; set; } = null!;

        public DbSet<Tenant> Tenants { get; set; } = null!;

        public DbSet<Subscription> Subscriptions { get; set; } = null!;

        public DbSet<UserMembership> UserMemberships { get; set; } = null!;

        public DbSet<OnboardingProcess> OnboardingProcesses { get; set; } = null!;

        public DbSet<CustomerActivationProcess> CustomerActivationProcesses { get; set; } = null!;

        public AccountContext(
            DbContextOptions<AccountContext> options,
            IMediator mediator,
            QltDbContextConfiguration configuration)
            : base(options, configuration)
        {
            System.Diagnostics.Debug.WriteLine("AccountContext::ctor ->" + this.GetHashCode());
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // Call base to apply Core configurations (Audit, Idempotency, etc.)
            base.OnModelCreating(modelBuilder);

            // Accounts-specific configurations are auto-discovered from AssembliesToRegister
            // No need to manually apply configurations here
        }
    }
}
