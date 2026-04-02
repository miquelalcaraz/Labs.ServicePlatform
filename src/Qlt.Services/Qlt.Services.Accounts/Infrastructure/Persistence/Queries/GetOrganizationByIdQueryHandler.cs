using Microsoft.EntityFrameworkCore;

using Qlt.Platform;
using Qlt.Platform.Messaging.Queries;
using Qlt.Services.Accounts.Application.Queries;

namespace Qlt.Services.Accounts.Infrastructure.Persistence.Queries;

/// <summary>
/// Handles GetOrganizationByIdQuery using the read-only AccountReadContext.
/// </summary>
public class GetOrganizationByIdQueryHandler(AccountReadContext context)
    : IQueryHandler<GetOrganizationByIdQuery, Organization>
{
    public async Task<Result<Organization>> Handle(GetOrganizationByIdQuery request, CancellationToken cancellationToken)
    {
        var organization = await context.Set<Domain.Models.OrganizationAggregate.Organization>()
            .FirstOrDefaultAsync(o => o.Id == request.Id, cancellationToken);

        if (organization is null)
            return Result.Failure<Organization>(Error.NotFound.WithMessage($"Organization with id '{request.Id}' was not found."));

        return Result.Success(new Organization
        {
            Id = organization.Id,
            TenantId = organization.TenantId,
            OrganizationType = organization.OrganizationType.ToString(),
            Name = organization.Name,
            LegalName = organization.LegalName,
            TaxNumber = organization.TaxNumber,
            TaxNumberType = organization.TaxNumberType.ToString(),
            Language = organization.Language ?? string.Empty,
            LanguageCode = organization.LanguageCode ?? string.Empty,
            CurrencyCode = organization.CurrencyCode ?? string.Empty,
            Address = new AddressViewModel
            {
                Country = organization.Address?.Country ?? string.Empty,
                CountryCode = organization.Address?.CountryCode ?? string.Empty,
                State = organization.Address?.State ?? string.Empty,
                City = organization.Address?.City ?? string.Empty,
                Street = organization.Address?.Street ?? string.Empty,
                PostalCode = organization.Address?.PostalCode ?? string.Empty
            }
        });
    }
}
