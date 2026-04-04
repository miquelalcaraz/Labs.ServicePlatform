using Qlt.Domain.SeedWork;
using Qlt.Services.Accounts.Domain.Models.UserMembershipAggregate;

namespace Qlt.Services.Accounts.Domain.Repositories;

public interface IUserMembershipRepository : IRepository<UserMembership>
{
    UserMembership Add(UserMembership membership);

    void Update(UserMembership membership);

    Task<UserMembership?> GetAsync(Guid membershipId);
}
