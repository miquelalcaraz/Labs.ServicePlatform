using QltS.Accounts.Domain.Models.OrganizationAggregate;
using QltS.Core.Domain.SeedWork;

namespace QltS.Accounts.Domain.Repositories
{
    public interface IOrganizationRepository : IRepository<Organization>
    {
        Organization Add(Organization Organization);

        void Update(Organization Organization);

        Task<Organization> GetAsync(Guid OrganizationId);
    }
}
