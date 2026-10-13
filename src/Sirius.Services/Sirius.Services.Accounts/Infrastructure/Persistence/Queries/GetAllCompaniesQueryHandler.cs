using Microsoft.EntityFrameworkCore;

using Sirius.Platform;
using Sirius.Platform.Messaging.Queries;
using Sirius.Services.Accounts.Application.Queries;

namespace Sirius.Services.Accounts.Infrastructure.Persistence.Queries;

/// <summary>
/// Handles GetAllCompaniesQuery using the read-only AccountReadContext.
/// </summary>
public class GetAllCompaniesQueryHandler(AccountReadContext context)
    : IQueryHandler<GetAllCompaniesQuery, IEnumerable<CompanySummary>>
{
    public async Task<Result<IEnumerable<CompanySummary>>> Handle(GetAllCompaniesQuery request, CancellationToken cancellationToken)
    {
        var companies = await context.Set<Domain.Models.CompanyAggregate.Company>()
            .Select(c => new CompanySummary
            {
                Id = c.Id,
                TenantId = c.TenantId,
                CompanyType = c.CompanyType.ToString(),
                Status = c.Status.ToString(),
                LegalName = c.LegalName,
                TradeName = c.TradeName,
                TaxId = c.TaxId,
                CountryCode = c.Address != null ? c.Address.CountryCode : string.Empty
            }).ToListAsync(cancellationToken);

        return Result.Success<IEnumerable<CompanySummary>>(companies);
    }
}
