using Microsoft.Extensions.Logging;

using Qlt.Platform;
using Qlt.Platform.Messaging.Commands;
using Qlt.Services.Accounts.Domain.Models.OrganizationAggregate;
using Qlt.Services.Accounts.Domain.Repositories;




namespace Qlt.Services.Accounts.Application.Commands;

public class CreateOrganizationCommandHandler : CommandHandler<CreateOrganizationCommand, bool>
{
    private readonly IOrganizationRepository _organizationRepository;
    private readonly ILogger<CreateOrganizationCommandHandler> _logger;

    public CreateOrganizationCommandHandler(
        IOrganizationRepository organizationRepository,
        ILogger<CreateOrganizationCommandHandler> logger)
    {
        _organizationRepository = organizationRepository ?? throw new ArgumentNullException(nameof(organizationRepository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    protected override async Task<Result<bool>> HandleAsync(CreateOrganizationCommand message, CancellationToken cancellationToken)
    {
        var address = Address.Create(message.Country, message.CountryCode, message.State, message.ZipCode, message.City!, message.Street);
        var organization = Organization.Create(message.Name, message.LegalName, address, message.TaxIdNumber, message.TaxNumberType, message.OrganizationType, message.Language, message.LanguageCode);
        _organizationRepository.Add(organization);
        //await Task.Delay(TimeSpan.FromSeconds(20));
        var saved = await _organizationRepository.UnitOfWork.SaveEntitiesAsync(cancellationToken);

        return saved
            ? Result.Success(true)
            : Result.Failure<bool>(Error.BadRequest.WithMessage("Failed to create organization"));
    }
}


public record OrganizationCreatedDTO
{
    public Guid Id { get; init; }
    public Guid TenantId { get; init; }
    public string Name { get; init; } = null!;
    public string LegalName { get; init; } = null!;
    public string? TaxIdNumber { get; init; } = null!;

    public string Country { get; init; } = null!;
    public string Language { get; init; } = null!;
    public string CountryCode { get; init; } = null!;
    public string LanguageCode { get; init; } = null!;


}