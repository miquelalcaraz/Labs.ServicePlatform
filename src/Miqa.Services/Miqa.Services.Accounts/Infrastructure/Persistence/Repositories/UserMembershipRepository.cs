using Miqa.Platform.Persistence.Repositories;
using Miqa.Services.Accounts.Domain.Models.UserMembershipAggregate;
using Miqa.Services.Accounts.Domain.Repositories;

namespace Miqa.Services.Accounts.Infrastructure.Persistence.Repositories;

public class UserMembershipRepository : Repository<UserMembership>, IUserMembershipRepository
{
    public UserMembershipRepository(AccountContext context) : base(context)
    {
    }
}
