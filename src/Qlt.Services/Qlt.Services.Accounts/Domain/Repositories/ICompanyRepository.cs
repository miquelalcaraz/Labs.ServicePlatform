using Qlt.Domain.SeedWork;
using Qlt.Services.Accounts.Domain.Models.CompanyAggregate;

namespace Qlt.Services.Accounts.Domain.Repositories;

public interface ICompanyRepository : IRepository<Company>
{
    Company Add(Company company);

    void Update(Company company);

    Task<Company?> GetAsync(Guid companyId);
}
