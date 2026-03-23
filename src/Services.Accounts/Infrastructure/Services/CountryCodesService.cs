using QltS.Accounts.Domain.Services;
using QltS.Shared;

namespace QltS.Accounts.Infrastructure.Services
{
    public class CountryCodesService : ICountryCodesService
    {
        public Task<Result<string>> GetCountryCodeAsync(string countryName, CancellationToken cancellationToken = default)
        {
            if (_countryCodes.TryGetValue(countryName, out var code))
            {
                return Task.FromResult(Result<string>.Success(code));
            }
            else
            {
                return Task.FromResult(Result.Failure<string>(Error.NotFound.WithMessage($"Country code for '{countryName}' not found.")));
            }
        }
        public Task<Result<string>> GetCountryNameAsync(string countryCode, CancellationToken cancellationToken = default)
        {
            var countryName = _countryCodes.FirstOrDefault(x => x.Value.Equals(countryCode, StringComparison.OrdinalIgnoreCase)).Key;
            if (countryName is not null)
            {
                return Task.FromResult(Result<string>.Success(countryName));
            }
            else
            {
                return Task.FromResult(Result.Failure<string>(Error.NotFound.WithMessage($"Country name for '{countryCode}' not found.")));
            }
        }
        public Task<Result<IEnumerable<string>>> GetAllCountryCodesAsync(CancellationToken cancellationToken = default)
        {
            IEnumerable<string> countryCodes = _countryCodes.Values.ToList();
            return Task.FromResult(Result<IEnumerable<string>>.Success(countryCodes));
        }
        public Task<Result<IEnumerable<string>>> GetAllCountryNamesAsync(CancellationToken cancellationToken = default)
        {
            IEnumerable<string> countryNames = _countryCodes.Keys.ToList();
            return Task.FromResult(Result<IEnumerable<string>>.Success(countryNames));
        }

        private Dictionary<string, string> _countryCodes = new Dictionary<string, string>
        {
            { "United States", "US" },
            { "Canada", "CA" },
            { "United Kingdom", "GB" },
            { "France", "FR" },
            { "Germany", "DE" },
            { "Italy", "IT" },
            { "Spain", "ES" },
            { "Japan", "JP" },
            { "China", "CN" },
            { "India", "IN" }
        };

    }
}
