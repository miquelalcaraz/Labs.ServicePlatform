using QltSystem.Core.Domain.SeedWork;

namespace QltSystem.Accounts.Domain.OrganizationAggregate
{
    public interface IOrganizationRepository : IRepository<Organization>
    {
        Organization Add(Organization Organization);

        void Update(Organization Organization);

        Task<Organization> GetAsync(Guid OrganizationId);
    }
}
