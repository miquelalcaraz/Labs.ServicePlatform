using QltSystem.Accounts.Domain.Models.OrganizationAggregate;
using QltSystem.Core.Domain.SeedWork;

namespace QltSystem.Accounts.Domain.Repositories
{
    public interface IOrganizationRepository : IRepository<Organization>
    {
        Organization Add(Organization Organization);

        void Update(Organization Organization);

        Task<Organization> GetAsync(Guid OrganizationId);
    }
}
