using QltSystem.Accounts.Domain.Services;
using QltSystem.Core.Domain.Exceptions;
using QltSystem.Core.Domain.SeedWork;

namespace QltSystem.Accounts.Infrastructure.Services
{
    public class LanguageCodesService : ILanguageCodesService
    {
        public Task<Result<string>> GetLanguageCodeAsync(string languageName, CancellationToken cancellationToken = default)
        {
            if (_languageCodes.TryGetValue(languageName, out var code))
            {
                return Task.FromResult(Result<string>.Success(code));
            }
            else
            {
                return Task.FromResult(Result.Failure<string>(Error.NotFound.WithMessage($"Language code for '{languageName}' not found.")));
            }

        }
        public Task<Result<string>> GetLanguageNameAsync(string languageCode, CancellationToken cancellationToken = default)
        {
            var languageName = _languageCodes.FirstOrDefault(x => x.Value.Equals(languageCode, StringComparison.OrdinalIgnoreCase)).Key;
            if (languageName is not null)
            {
                return Task.FromResult(Result<string>.Success(languageName));
            }
            else
            {
                return Task.FromResult(Result.Failure<string>(Error.NotFound.WithMessage($"Language name for '{languageCode}' not found.")));
            }
        }
        public Task<Result<IEnumerable<string>>> GetAllLanguageCodesAsync(CancellationToken cancellationToken = default)
        {
            IEnumerable<string> languageCodes = _languageCodes.Values.ToList();
            return Task.FromResult(Result<IEnumerable<string>>.Success(languageCodes));
        }
        public Task<Result<IEnumerable<string>>> GetAllLanguageNamesAsync(CancellationToken cancellationToken = default)
        {
            IEnumerable<string> languageNames = _languageCodes.Keys.ToList();
            return Task.FromResult(Result<IEnumerable<string>>.Success(languageNames));
        }


        private Dictionary<string, string> _languageCodes = new Dictionary<string, string>
        {
            { "English", "en" },
            { "French", "fr" },
            { "Spanish", "es" },
            { "German", "de" },
            { "Italian", "it" },
            { "Portuguese", "pt" },
            { "Russian", "ru" },
            { "Chinese", "zh" },
            { "Japanese", "ja" },
            { "Korean", "ko" }
        };
    }
}
