using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;

using Qlt.BackOfficeApp.Models;

using System.Security.Claims;

namespace Qlt.BackOfficeApp.Services
{
    public partial class SecurityService
    {
        private readonly HttpClient httpClient;
        private readonly HttpClient identityHttpClient;
        private readonly Uri identityBaseUri;
        private readonly NavigationManager navigationManager;

        public string UserName => Principal?.FindFirst("email")?.Value;
        public ClaimsPrincipal Principal { get; private set; }

        public SecurityService(NavigationManager navigationManager, IHttpClientFactory factory, IConfiguration configuration)
        {
            this.identityBaseUri = new Uri(configuration["Identity:Url"]!.TrimEnd('/'));
            this.identityHttpClient = factory.CreateClient("IdentityServer");
            this.httpClient = factory.CreateClient("QltBackOffice");
            this.navigationManager = navigationManager;
        }

        public bool IsInRole(params string[] roles)
        {
#if DEBUG
            if (Principal?.FindFirst("email")?.Value == "admin")
            {
                return true;
            }
#endif

            if (roles.Contains("Everybody"))
            {
                return true;
            }

            if (!IsAuthenticated())
            {
                return false;
            }

            if (roles.Contains("Authenticated"))
            {
                return true;
            }

            return roles.Any(role => Principal.IsInRole(role));
        }

        public bool IsAuthenticated()
        {
            return Principal?.Identity.IsAuthenticated == true;
        }

        public Task<bool> InitializeAsync(AuthenticationState result)
        {
            Principal = result.User;
            return Task.FromResult(IsAuthenticated());
        }

        public async Task<ApplicationAuthenticationState> GetAuthenticationStateAsync()
        {
            var uri = new Uri($"{navigationManager.BaseUri}auth/CurrentUser");

            var response = await httpClient.SendAsync(new HttpRequestMessage(HttpMethod.Post, uri));

            if (!response.IsSuccessStatusCode)
            {
                return new ApplicationAuthenticationState { IsAuthenticated = false };
            }

            return await response.Content.ReadFromJsonAsync<ApplicationAuthenticationState>();
        }

        public void Logout()
        {
            navigationManager.NavigateTo("auth/Logout", true);
        }

        public void Login()
        {
            navigationManager.NavigateTo("auth/Login", true);
        }

        public async Task ChangePassword(string oldPassword, string newPassword)
        {
            var uri = new Uri($"{identityBaseUri.GetLeftPart(UriPartial.Authority)}/Account/ChangePassword");

            var content = new FormUrlEncodedContent(new Dictionary<string, string> {
                { "oldPassword", oldPassword },
                { "newPassword", newPassword }
            });

            var response = await identityHttpClient.PostAsync(uri, content);

            if (!response.IsSuccessStatusCode)
            {
                var message = await response.Content.ReadAsStringAsync();

                throw new ApplicationException(message);
            }
        }
    }
}