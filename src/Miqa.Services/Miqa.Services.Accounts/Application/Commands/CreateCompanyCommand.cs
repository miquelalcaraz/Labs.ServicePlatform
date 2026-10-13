using System.Runtime.Serialization;

using Miqa.Platform;
using Miqa.Platform.Messaging.Commands;
using Miqa.Services.Accounts.Domain.Models.CompanyAggregate;

namespace Miqa.Services.Accounts.Application.Commands;

[DataContract]
public record CreateCompanyCommand : IdempotentCommand<bool>
{
    [DataMember]
    public Guid TenantId { get; private set; }

    [DataMember]
    public string LegalName { get; private set; } = null!;

    [DataMember]
    public string TradeName { get; private set; } = null!;

    [DataMember]
    public CompanyType CompanyType { get; private set; } = CompanyType.Owner;

    [DataMember]
    public string TaxId { get; private set; } = null!;

    [DataMember]
    public TaxNumberType TaxNumberType { get; private set; }

    [DataMember]
    public string Country { get; private set; } = null!;

    [DataMember]
    public string CountryCode { get; private set; } = null!;

    [DataMember]
    public string? State { get; private set; }

    [DataMember]
    public string? PostalCode { get; private set; }

    [DataMember]
    public string? City { get; private set; }

    [DataMember]
    public string? Street { get; private set; }

    public CreateCompanyCommand() { }

    public CreateCompanyCommand(Guid? requestId = null) : base(requestId) { }

    public CreateCompanyCommand(
        Guid tenantId,
        string legalName,
        string tradeName,
        string taxId,
        string country,
        string countryCode,
        CompanyType companyType = CompanyType.Owner,
        TaxNumberType taxNumberType = TaxNumberType.TaxId,
        string? state = null,
        string? postalCode = null,
        string? city = null,
        string? street = null,
        Guid? requestId = null)
        : base(requestId)
    {
        TenantId = tenantId;
        LegalName = legalName;
        TradeName = tradeName;
        TaxId = taxId;
        Country = country;
        CountryCode = countryCode;
        CompanyType = companyType;
        TaxNumberType = taxNumberType;
        State = state;
        PostalCode = postalCode;
        City = city;
        Street = street;
    }

    public override Result<bool> CreateResultForDuplicateRequest()
    {
        return Result.Success(true);
    }
}
