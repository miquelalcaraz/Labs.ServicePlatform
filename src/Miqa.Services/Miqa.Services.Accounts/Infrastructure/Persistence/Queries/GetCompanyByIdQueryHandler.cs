using Microsoft.EntityFrameworkCore;

using Miqa.Platform;
using Miqa.Platform.Messaging.Queries;
using Miqa.Services.Accounts.Application.Queries;

namespace Miqa.Services.Accounts.Infrastructure.Persistence.Queries;

/// <summary>
/// Handles GetCompanyByIdQuery using the read-only AccountReadContext.
/// </summary>
public class GetCompanyByIdQueryHandler(AccountReadContext context)
    : IQueryHandler<GetCompanyByIdQuery, CompanyDetail>
{
    public async Task<Result<CompanyDetail>> Handle(GetCompanyByIdQuery request, CancellationToken cancellationToken)
    {
        var company = await context.Set<Domain.Models.CompanyAggregate.Company>()
            .FirstOrDefaultAsync(c => c.Id == request.Id, cancellationToken);

        if (company is null)
            return Result.Failure<CompanyDetail>(Error.NotFound.WithMessage($"Company with id '{request.Id}' was not found."));

        return Result.Success(new CompanyDetail
        {
            Id = company.Id,
            TenantId = company.TenantId,
            CompanyType = company.CompanyType.ToString(),
            Status = company.Status.ToString(),
            LegalName = company.LegalName,
            TradeName = company.TradeName,
            TaxId = company.TaxId,
            TaxNumberType = company.TaxNumberType.ToString(),
            Address = new AddressViewModel
            {
                Country = company.Address?.Country ?? string.Empty,
                CountryCode = company.Address?.CountryCode ?? string.Empty,
                State = company.Address?.State ?? string.Empty,
                City = company.Address?.City ?? string.Empty,
                Street = company.Address?.Street ?? string.Empty,
                PostalCode = company.Address?.PostalCode ?? string.Empty
            }
        });
    }
}
