using QltS.Accounts.Domain.Models.OrganizationAggregate;
using QltS.Accounts.Domain.Repositories;
using QltS.Shared.Persistence.EF.Relational.Repositories;

namespace QltS.Accounts.Infrastructure.Persistence.Repositories
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
