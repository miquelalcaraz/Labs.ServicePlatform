using Duende.IdentityServer.Models;

namespace Miqa.Infrastructure.Identity.Infrastructure.Seed
{
    /// <summary>
    /// Builds the external OIDC identity providers from the <c>ExternalProviders</c> configuration section.
    /// Credentials are never stored in source code: supply them through user secrets or environment variables.
    /// </summary>
    internal static class ProviderSeeding
    {
        private const string SectionName = "ExternalProviders";

        public static IEnumerable<IdentityProvider> Get(IConfiguration configuration)
        {
            var providers = configuration.GetSection(SectionName).Get<List<ExternalProviderOptions>>() ?? [];

            return providers
                .Where(options => !string.IsNullOrWhiteSpace(options.Scheme))
                .Select(ToIdentityProvider)
                .ToList();
        }

        private static IdentityProvider ToIdentityProvider(ExternalProviderOptions options)
        {
            var oidcProvider = new OidcProvider
            {
                Scheme = options.Scheme,
                DisplayName = options.DisplayName ?? options.Scheme,
                Authority = options.Authority,
                ClientId = options.ClientId,
                ClientSecret = options.ClientSecret,
                ResponseType = "code",
                Scope = options.Scope ?? "openid profile email"
            };

            var isConfigured = !string.IsNullOrWhiteSpace(options.Authority)
                && !string.IsNullOrWhiteSpace(options.ClientId)
                && !string.IsNullOrWhiteSpace(options.ClientSecret);

            return new IdentityProvider(oidcProvider.Type, oidcProvider)
            {
                Enabled = options.Enabled && isConfigured
            };
        }

        private sealed class ExternalProviderOptions
        {
            public string Scheme { get; set; } = string.Empty;
            public string? DisplayName { get; set; }
            public string? Authority { get; set; }
            public string? ClientId { get; set; }
            public string? ClientSecret { get; set; }
            public string? Scope { get; set; }
            public bool Enabled { get; set; }
        }
    }
}
