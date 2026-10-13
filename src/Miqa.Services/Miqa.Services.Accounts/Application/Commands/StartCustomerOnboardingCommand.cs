using System.Runtime.Serialization;

using Miqa.Platform;
using Miqa.Platform.Messaging.Commands;
using Miqa.Services.Accounts.Domain.Models.CompanyAggregate;

namespace Miqa.Services.Accounts.Application.Commands;

[DataContract]
public record StartCustomerOnboardingCommand : IdempotentCommand<bool>
{
    [DataMember]
    public string PlanCode { get; private set; } = null!;

    [DataMember]
    public string? TenantName { get; private set; }

    [DataMember]
    public string CompanyLegalName { get; private set; } = null!;

    [DataMember]
    public string CompanyTradeName { get; private set; } = null!;

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

    [DataMember]
    public string AdminEmail { get; private set; } = null!;

    [DataMember]
    public string AdminFirstName { get; private set; } = null!;

    [DataMember]
    public string AdminLastName { get; private set; } = null!;

    [DataMember]
    public Guid CreatedByInternalUserId { get; private set; }

    public StartCustomerOnboardingCommand() { }

    public StartCustomerOnboardingCommand(Guid? requestId = null) : base(requestId) { }

    public StartCustomerOnboardingCommand(
        string planCode,
        string companyLegalName,
        string companyTradeName,
        string taxId,
        TaxNumberType taxNumberType,
        string country,
        string countryCode,
        string adminEmail,
        string adminFirstName,
        string adminLastName,
        Guid createdByInternalUserId,
        string? tenantName = null,
        string? state = null,
        string? postalCode = null,
        string? city = null,
        string? street = null,
        Guid? requestId = null)
        : base(requestId)
    {
        PlanCode = planCode;
        TenantName = tenantName;
        CompanyLegalName = companyLegalName;
        CompanyTradeName = companyTradeName;
        TaxId = taxId;
        TaxNumberType = taxNumberType;
        Country = country;
        CountryCode = countryCode;
        State = state;
        PostalCode = postalCode;
        City = city;
        Street = street;
        AdminEmail = adminEmail;
        AdminFirstName = adminFirstName;
        AdminLastName = adminLastName;
        CreatedByInternalUserId = createdByInternalUserId;
    }

    public override Result<bool> CreateResultForDuplicateRequest()
    {
        return Result.Success(true);
    }
}
