using QltS.Shared;

namespace QltS.Accounts.Domain.Services
{
    public interface ICountryCodesService
    {
        Task<Result<string>> GetCountryCodeAsync(string countryName, CancellationToken cancellationToken = default);
        Task<Result<string>> GetCountryNameAsync(string countryCode, CancellationToken cancellationToken = default);
        Task<Result<IEnumerable<string>>> GetAllCountryCodesAsync(CancellationToken cancellationToken = default);
        Task<Result<IEnumerable<string>>> GetAllCountryNamesAsync(CancellationToken cancellationToken = default);
    }



}
