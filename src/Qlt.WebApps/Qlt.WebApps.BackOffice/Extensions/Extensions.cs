using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Server;
using Microsoft.IdentityModel.JsonWebTokens;

using Qlt.Platform;
using Qlt.WebApps.BackOffice.Services;

namespace Qlt.WebApps.BackOffice.Extensions
{
    public static class Extensions
    {
        public static void AddApplicationServices(this IHostApplicationBuilder builder)
        {
            builder.AddAuthenticationServices();
            builder.Services.AddHttpForwarderWithServiceDiscovery();

            // Application services
            builder.Services.AddScoped<LogOutService>();
        }

        public static void AddAuthenticationServices(this IHostApplicationBuilder builder)
        {
            var configuration = builder.Configuration;
            var services = builder.Services;

            JsonWebTokenHandler.DefaultInboundClaimTypeMap.Remove("sub");

            var identityUrl = configuration.GetRequiredValue("IdentityUrl");
            var callBackUrl = configuration.GetRequiredValue("CallBackUrl");
            var sessionCookieLifetime = configuration.GetValue("SessionCookieLifetimeMinutes", 60);

            // Add Authentication services
            services.AddAuthorization();
            services.AddAuthentication(options =>
            {
                options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = OpenIdConnectDefaults.AuthenticationScheme;
            })
            .AddCookie(options => options.ExpireTimeSpan = TimeSpan.FromMinutes(sessionCookieLifetime))
            .AddOpenIdConnect(options =>
            {
                options.SignInScheme = CookieAuthenticationDefaults.AuthenticationScheme;
                options.Authority = identityUrl;
                options.SignedOutRedirectUri = callBackUrl;
                options.ClientId = "webapp";
                options.ClientSecret = "secret";
                options.ResponseType = "code";
                options.SaveTokens = true;
                options.GetClaimsFromUserInfoEndpoint = true;
                options.RequireHttpsMetadata = false;
                options.Scope.Add("openid");
                options.Scope.Add("profile");
                options.Scope.Add("role");
                options.Scope.Add("accounts");
            });

            // Blazor auth services
            services.AddScoped<AuthenticationStateProvider, ServerAuthenticationStateProvider>();
            services.AddCascadingAuthenticationState();
        }

        public static async Task<string?> GetUserIdAsync(this AuthenticationStateProvider authenticationStateProvider)
        {
            var authState = await authenticationStateProvider.GetAuthenticationStateAsync();
            var user = authState.User;
            return user.FindFirst("sub")?.Value;
        }

        public static async Task<string?> GetUserNameAsync(this AuthenticationStateProvider authenticationStateProvider)
        {
            var authState = await authenticationStateProvider.GetAuthenticationStateAsync();
            var user = authState.User;
            return user.FindFirst("name")?.Value;
        }
    }
}
