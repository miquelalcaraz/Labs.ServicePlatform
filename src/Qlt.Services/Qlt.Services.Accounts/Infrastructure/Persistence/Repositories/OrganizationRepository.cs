using Qlt.Platform.Persistence.Repositories;
using Qlt.Services.Accounts.Domain.Models.OrganizationAggregate;
using Qlt.Services.Accounts.Domain.Repositories;

namespace Qlt.Services.Accounts.Infrastructure.Persistence.Repositories
{
    public class OrganizationRepository : Repository<Organization>, IOrganizationRepository
    {
        public OrganizationRepository(AccountContext context) : base(context)
        {
        }
    }
}
