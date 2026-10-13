namespace Sirius.Services.Accounts.Application.Queries;

public record CompanyDetail
{
    public Guid Id { get; init; }
    public Guid TenantId { get; init; }
    public string CompanyType { get; init; } = null!;
    public string Status { get; init; } = null!;
    public string LegalName { get; init; } = null!;
    public string TradeName { get; init; } = null!;
    public string TaxId { get; init; } = null!;
    public string TaxNumberType { get; init; } = null!;
    public AddressViewModel Address { get; init; } = null!;
}

public record CompanySummary
{
    public Guid Id { get; init; }
    public Guid TenantId { get; init; }
    public string CompanyType { get; init; } = null!;
    public string Status { get; init; } = null!;
    public string LegalName { get; init; } = null!;
    public string TradeName { get; init; } = null!;
    public string TaxId { get; init; } = null!;
    public string CountryCode { get; init; } = string.Empty;
}

public record AddressViewModel
{
    public string Country { get; init; } = string.Empty;
    public string CountryCode { get; init; } = string.Empty;
    public string State { get; init; } = string.Empty;
    public string City { get; init; } = string.Empty;
    public string Street { get; init; } = string.Empty;
    public string PostalCode { get; init; } = string.Empty;
}
