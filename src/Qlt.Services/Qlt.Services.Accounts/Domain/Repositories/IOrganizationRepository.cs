using Qlt.Domain.SeedWork;
using Qlt.Services.Accounts.Domain.Models.OrganizationAggregate;

namespace Qlt.Services.Accounts.Domain.Repositories
{
    public interface IOrganizationRepository : IRepository<Organization>
    {
        Organization Add(Organization Organization);

        void Update(Organization Organization);

        Task<Organization?> GetAsync(Guid OrganizationId);
    }
}
