using Microsoft.EntityFrameworkCore;

using Qlt.Platform;
using Qlt.Platform.Messaging.Queries;
using Qlt.Services.Accounts.Application.Queries;

namespace Qlt.Services.Accounts.Infrastructure.Persistence.Queries;

/// <summary>
/// Handles GetAllOrganizationsQuery using the read-only AccountReadContext.
/// </summary>
public class GetAllOrganizationsQueryHandler(AccountReadContext context)
    : IQueryHandler<GetAllOrganizationsQuery, IEnumerable<OrganizationSummary>>
{
    public async Task<Result<IEnumerable<OrganizationSummary>>> Handle(GetAllOrganizationsQuery request, CancellationToken cancellationToken)
    {
        var organizations = await context.Set<Domain.Models.OrganizationAggregate.Organization>()
            .Select(o => new OrganizationSummary
            {
                Id = o.Id,
                TenantId = o.TenantId,
                OrganizationType = o.OrganizationType.ToString(),
                Name = o.Name,
                LegalName = o.LegalName,
                TaxNumber = o.TaxNumber,
                TaxNumberType = o.TaxNumberType.ToString(),
                Country = o.Address != null ? o.Address.Country : string.Empty,
                CountryCode = o.Address != null ? o.Address.CountryCode : string.Empty
            }).ToListAsync(cancellationToken);

        return Result.Success<IEnumerable<OrganizationSummary>>(organizations);
    }
}
