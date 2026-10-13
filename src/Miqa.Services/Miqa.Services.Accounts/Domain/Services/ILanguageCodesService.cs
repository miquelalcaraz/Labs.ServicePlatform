using Miqa.Platform;

namespace Miqa.Services.Accounts.Domain.Services
{
    public interface ILanguageCodesService
    {
        Task<Result<string>> GetLanguageCodeAsync(string languageName, CancellationToken cancellationToken = default);
        Task<Result<string>> GetLanguageNameAsync(string languageCode, CancellationToken cancellationToken = default);
        Task<Result<IEnumerable<string>>> GetAllLanguageCodesAsync(CancellationToken cancellationToken = default);
        Task<Result<IEnumerable<string>>> GetAllLanguageNamesAsync(CancellationToken cancellationToken = default);

    }



}
