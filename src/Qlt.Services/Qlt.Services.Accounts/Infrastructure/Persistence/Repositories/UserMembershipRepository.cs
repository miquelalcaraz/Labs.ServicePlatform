using Qlt.Platform.Persistence.Repositories;
using Qlt.Services.Accounts.Domain.Models.UserMembershipAggregate;
using Qlt.Services.Accounts.Domain.Repositories;

namespace Qlt.Services.Accounts.Infrastructure.Persistence.Repositories;

public class UserMembershipRepository : Repository<UserMembership>, IUserMembershipRepository
{
    public UserMembershipRepository(AccountContext context) : base(context)
    {
    }
}
