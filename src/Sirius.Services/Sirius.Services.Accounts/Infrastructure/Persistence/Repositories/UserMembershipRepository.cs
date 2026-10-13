using Sirius.Platform.Persistence.Repositories;
using Sirius.Services.Accounts.Domain.Models.UserMembershipAggregate;
using Sirius.Services.Accounts.Domain.Repositories;

namespace Sirius.Services.Accounts.Infrastructure.Persistence.Repositories;

public class UserMembershipRepository : Repository<UserMembership>, IUserMembershipRepository
{
    public UserMembershipRepository(AccountContext context) : base(context)
    {
    }
}
