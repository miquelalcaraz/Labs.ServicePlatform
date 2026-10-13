using Microsoft.Extensions.Logging;

using Sirius.Platform;
using Sirius.Platform.Messaging.Commands;
using Sirius.Services.Accounts.Domain.Models.CompanyAggregate;
using Sirius.Services.Accounts.Domain.Repositories;

namespace Sirius.Services.Accounts.Application.Commands;

public class CreateCompanyCommandHandler : ICommandHandler<CreateCompanyCommand, bool>
{
    private readonly ICompanyRepository _companyRepository;
    private readonly ILogger<CreateCompanyCommandHandler> _logger;

    public CreateCompanyCommandHandler(
        ICompanyRepository companyRepository,
        ILogger<CreateCompanyCommandHandler> logger)
    {
        _companyRepository = companyRepository ?? throw new ArgumentNullException(nameof(companyRepository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<Result<bool>> Handle(CreateCompanyCommand message, CancellationToken cancellationToken)
    {
        var address = Address.Create(
            message.Country, message.CountryCode, message.State,
            message.PostalCode, message.City!, message.Street);

        var company = Company.Create(
            message.TenantId, message.LegalName, message.TradeName,
            message.CompanyType, address, message.TaxId, message.TaxNumberType);

        _companyRepository.Add(company);
        var saved = await _companyRepository.UnitOfWork.SaveChangesAsync(cancellationToken);

        return saved > 0
            ? Result.Success(true)
            : Result.Failure<bool>(Error.BadRequest.WithMessage("Failed to create company"));
    }
}
