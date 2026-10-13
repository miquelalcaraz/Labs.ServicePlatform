using Sirius.Platform.Persistence.Repositories;
using Sirius.Services.Accounts.Domain.Models.CompanyAggregate;
using Sirius.Services.Accounts.Domain.Repositories;

namespace Sirius.Services.Accounts.Infrastructure.Persistence.Repositories;

public class CompanyRepository : Repository<Company>, ICompanyRepository
{
    public CompanyRepository(AccountContext context) : base(context)
    {
    }
}
