using Sirius.Domain.SeedWork;
using Sirius.Services.Accounts.Domain.Models.UserMembershipAggregate;

namespace Sirius.Services.Accounts.Domain.Repositories;

public interface IUserMembershipRepository : IRepository<UserMembership>
{
    UserMembership Add(UserMembership membership);

    void Update(UserMembership membership);

    Task<UserMembership?> GetAsync(Guid membershipId);
}
