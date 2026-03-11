using Core.Persistence.EF.Relational.Repositories;

using QltSystem.Accounts.Domain.Models.OrganizationAggregate;
using QltSystem.Accounts.Domain.Repositories;

namespace QltSystem.Accounts.Infrastructure.Persistence.Repositories
{
    public class OrganizationRepository : Repository<Organization>, IOrganizationRepository
    {
        public OrganizationRepository(AccountContext context) : base(context)
        {
        }

        // Métodos específicos de OrganizationRepository si los hay
        // Los métodos base (Add, Update, GetAsync) ya están implementados en Repository<T>
    }
}
