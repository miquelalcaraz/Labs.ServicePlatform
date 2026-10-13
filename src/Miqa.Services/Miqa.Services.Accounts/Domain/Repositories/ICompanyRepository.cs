using Miqa.Domain.SeedWork;
using Miqa.Services.Accounts.Domain.Models.CompanyAggregate;

namespace Miqa.Services.Accounts.Domain.Repositories;

public interface ICompanyRepository : IRepository<Company>
{
    Company Add(Company company);

    void Update(Company company);

    Task<Company?> GetAsync(Guid companyId);
}
