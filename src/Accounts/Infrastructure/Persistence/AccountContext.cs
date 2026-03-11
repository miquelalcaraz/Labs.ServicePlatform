using Core.Persistence.EF.Relational;

using MediatR;

using Microsoft.EntityFrameworkCore;

using QltSystem.Accounts.Domain.Models.OrganizationAggregate;
using QltSystem.Core.Persistence.EF.Extensions;

namespace QltSystem.Accounts.Infrastructure.Persistence
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
            : base(options, mediator, configuration)
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
