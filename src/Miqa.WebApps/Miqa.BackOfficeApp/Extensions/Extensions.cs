using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.IdentityModel.Tokens;

using Miqa.BackOfficeApp.Services;
using Miqa.Platform.Builders;
using Miqa.Platform.Extensions;

namespace Miqa.BackOfficeApp.Extensions
{
    public static class Extensions
    {
        public static void AddApplicationServices(this IHostApplicationBuilder builder)
        {
            builder.AddAuthenticationServices();

            builder.Services.AddHttpClient<OnboardingApiService>(o =>
                o.BaseAddress = new("https+http://backoffice-bff"))
                .AddApiVersion(1.0)
                .AddAuthToken();

            builder.Services.AddHttpClient<ActivationApiService>(o =>
                o.BaseAddress = new("https+http://backoffice-bff"))
                .AddApiVersion(1.0)
                .AddAuthToken();

            builder.Services.AddHttpClient("Miqa.BackOfficeApp")
                .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { UseCookies = false })
                .AddHeaderPropagation(o => o.Headers.Add("Cookie"));
            builder.Services.AddHeaderPropagation(o => o.Headers.Add("Cookie"));

        }
        public static void AddAuthenticationServices(this IHostApplicationBuilder builder)
        {
            var configuration = builder.Configuration;
            var identityAuthority = configuration.GetRequiredValue("Identity:Authority");
            var identityUrl = configuration.GetRequiredValue("Identity:Url");
            var callBackUrl = configuration.GetRequiredValue("CallBackUrl");
            var sessionCookieLifetime = configuration.GetValue("SessionCookieLifetimeMinutes", 5);

            builder.Services.AddHttpClient("IdentityServer", client =>
            {
                client.BaseAddress = new Uri(identityUrl);
            }).ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { UseCookies = false }).AddHeaderPropagation(o => o.Headers.Add("Cookie"));

            builder.Services.AddAuthentication(options =>
            {
                options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = OpenIdConnectDefaults.AuthenticationScheme;
            })
           .AddCookie(options => options.ExpireTimeSpan = TimeSpan.FromMinutes(sessionCookieLifetime))
            .AddOpenIdConnect(options =>
            {
                options.Authority = identityAuthority;
                options.ClientId = builder.Configuration["Identity:ClientId"];
                options.ClientSecret = builder.Configuration["Identity:ClientSecret"];
                options.ResponseType = "code";
                options.SaveTokens = true;
                options.GetClaimsFromUserInfoEndpoint = true;
                options.MapInboundClaims = false;
                options.Scope.Clear();
                foreach (var scope in builder.Configuration["Identity:Scopes"]!.Split(' '))
                {
                    options.Scope.Add(scope);
                }
                options.CallbackPath = "/signin-oidc";
                options.SignedOutCallbackPath = "/signout-callback-oidc";
                options.PushedAuthorizationBehavior = PushedAuthorizationBehavior.Disable;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    NameClaimType = "name",
                    RoleClaimType = "role"
                };
            });
            builder.Services.AddAuthorization();
            builder.Services.AddScoped<SecurityService>();
            builder.Services.AddScoped<AuthenticationStateProvider, ApplicationAuthenticationStateProvider>();
            builder.Services.AddCascadingAuthenticationState();
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
