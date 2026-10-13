using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;

using Sirius.BackOfficeApp.Models;

namespace Sirius.BackOfficeApp.Api
{
    public static class AuthApi
    {
        public static IEndpointRouteBuilder MapAuthApi(this IEndpointRouteBuilder app)
        {
            app.MapGet("auth/Login", (HttpContext httpContext, string returnUrl = "/") =>
            {
                // Only allow local return URLs to prevent open redirects
                if (!IsLocalUrl(returnUrl))
                {
                    returnUrl = "/";
                }

                if (httpContext.User.Identity?.IsAuthenticated == true)
                {
                    return Results.Redirect(returnUrl);
                }
                return Results.Challenge(
                    new AuthenticationProperties { RedirectUri = returnUrl },
                    [OpenIdConnectDefaults.AuthenticationScheme]);
            });

            app.MapPost("auth/CurrentUser", (HttpContext httpContext) =>
            {
                return new ApplicationAuthenticationState
                {
                    IsAuthenticated = httpContext.User.Identity?.IsAuthenticated == true,
                    Name = httpContext.User.Identity?.Name,
                    Claims = httpContext.User.Claims.Select(c => new ApplicationClaim { Type = c.Type, Value = c.Value })
                };
            });

            app.MapGet("auth/Logout", () =>
            {
                return Results.SignOut(
                    new AuthenticationProperties { RedirectUri = "/" },
                    [CookieAuthenticationDefaults.AuthenticationScheme, OpenIdConnectDefaults.AuthenticationScheme]);
            });
            return app;
        }

        private static bool IsLocalUrl(string? url) =>
            !string.IsNullOrEmpty(url)
            && url[0] == '/'
            && (url.Length == 1 || (url[1] != '/' && url[1] != '\\'));
    }
}
