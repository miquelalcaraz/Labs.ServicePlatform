using Qlt.Platform.Persistence.Repositories;
using Qlt.Services.Accounts.Domain.Models.CompanyAggregate;
using Qlt.Services.Accounts.Domain.Repositories;

namespace Qlt.Services.Accounts.Infrastructure.Persistence.Repositories;

public class CompanyRepository : Repository<Company>, ICompanyRepository
{
    public CompanyRepository(AccountContext context) : base(context)
    {
    }
}
