using System.Net.Http.Headers;

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

using Qlt.Platform.Services;

namespace Qlt.Platform.Builders;

public static class HttpClientExtensions
{
    public static IHttpClientBuilder AddAuthToken(this IHttpClientBuilder builder)
    {
        builder.Services.AddHttpContextAccessor();
        builder.Services.TryAddScoped<TokenProvider>();
        builder.Services.TryAddTransient<HttpClientAuthorizationDelegatingHandler>();
        builder.AddHttpMessageHandler<HttpClientAuthorizationDelegatingHandler>();

        return builder;
    }

    private class HttpClientAuthorizationDelegatingHandler : DelegatingHandler
    {
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly TokenProvider _tokenProvider;

        public HttpClientAuthorizationDelegatingHandler(IHttpContextAccessor httpContextAccessor, TokenProvider tokenProvider)
        {
            _httpContextAccessor = httpContextAccessor;
            _tokenProvider = tokenProvider;
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var accessToken = _tokenProvider.AccessToken;

            if (accessToken is null && _httpContextAccessor.HttpContext is HttpContext context)
            {
                accessToken = await context.GetTokenAsync("access_token");
                _tokenProvider.AccessToken = accessToken;
            }

            if (accessToken is not null)
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            }

            return await base.SendAsync(request, cancellationToken);
        }
    }
}
