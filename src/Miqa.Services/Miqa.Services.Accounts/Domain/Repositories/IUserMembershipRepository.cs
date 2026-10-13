using Miqa.Domain.SeedWork;
using Miqa.Services.Accounts.Domain.Models.UserMembershipAggregate;

namespace Miqa.Services.Accounts.Domain.Repositories;

public interface IUserMembershipRepository : IRepository<UserMembership>
{
    UserMembership Add(UserMembership membership);

    void Update(UserMembership membership);

    Task<UserMembership?> GetAsync(Guid membershipId);
}
