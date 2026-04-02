using MediatR;

using Microsoft.EntityFrameworkCore;

using Qlt.Platform.Persistence;
using Qlt.Platform.Persistence.Options;
using Qlt.Services.Accounts.Domain.Models.OrganizationAggregate;

namespace Qlt.Services.Accounts.Infrastructure.Persistence
{
    /// <summary>
    /// Database context for Accounts bounded context.
    /// Manages Organization aggregate and related entities.
    /// </summary>
    public class AccountContext : QltDBContext
    {
        /// <summary>
        /// Organizations aggregate root
        /// </summary>
        public DbSet<Organization> Organizations { get; set; } = null!;

        /// <summary>
        /// Organization contacts
        /// </summary>
        public DbSet<OrganizationContact> OrganizationContacts { get; set; } = null!;

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
