using Miqa.Platform.Persistence.Repositories;
using Miqa.Services.Accounts.Domain.Models.CompanyAggregate;
using Miqa.Services.Accounts.Domain.Repositories;

namespace Miqa.Services.Accounts.Infrastructure.Persistence.Repositories;

public class CompanyRepository : Repository<Company>, ICompanyRepository
{
    public CompanyRepository(AccountContext context) : base(context)
    {
    }
}
