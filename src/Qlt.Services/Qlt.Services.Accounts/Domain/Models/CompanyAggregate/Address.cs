using Qlt.Domain.SeedWork;

namespace Qlt.Services.Accounts.Domain.Models.CompanyAggregate;

public class Address : ValueObject
{
    public string Country { get; private set; } = null!;
    public string CountryCode { get; internal set; } = null!;
    public string? State { get; private set; }
    public string? PostalCode { get; private set; }
    public string City { get; private set; } = null!;
    public string? Street { get; private set; }

    protected Address() { }

    public static Address Create(string country, string countryCode, string? state, string? postalCode, string city, string? street)
    {
        return new Address
        {
            Street = street,
            City = city,
            State = state,
            Country = country,
            PostalCode = postalCode,
            CountryCode = countryCode
        };
    }

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return Street ?? string.Empty;
        yield return City;
        yield return State ?? string.Empty;
        yield return Country;
        yield return PostalCode ?? string.Empty;
        yield return CountryCode;
    }
}
