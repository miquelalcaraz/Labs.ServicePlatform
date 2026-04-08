using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Web;

using Qlt.BackOfficeApp.Models;

using System.Security.Claims;

namespace Qlt.BackOfficeApp.Services
{
    public class ApplicationAuthenticationStateProvider : AuthenticationStateProvider
    {
        private const string AuthenticationStateKey = "QltBackOffice.AuthenticationState";
        private readonly SecurityService securityService;
        private readonly PersistentComponentState persistentComponentState;
        private ApplicationAuthenticationState authenticationState;

        public ApplicationAuthenticationStateProvider(SecurityService securityService, PersistentComponentState persistentComponentState)
        {
            this.securityService = securityService;
            this.persistentComponentState = persistentComponentState;
        }

        public override async Task<AuthenticationState> GetAuthenticationStateAsync()
        {
            var identity = new ClaimsIdentity();

            try
            {
                var state = await GetApplicationAuthenticationStateAsync();

                if (state.IsAuthenticated)
                {
                    identity = new ClaimsIdentity(state.Claims.Select(c => new Claim(c.Type, c.Value)), "QltBackOffice");
                }
            }
            catch (HttpRequestException ex)
            {
            }

            var result = new AuthenticationState(new ClaimsPrincipal(identity));

            await securityService.InitializeAsync(result);

            return result;
        }

        private async Task<ApplicationAuthenticationState> GetApplicationAuthenticationStateAsync()
        {
            if (persistentComponentState.TryTakeFromJson<ApplicationAuthenticationState>(AuthenticationStateKey, out var persistedState))
            {
                authenticationState = persistedState;
            }
            else if (authenticationState == null)
            {
                authenticationState = await securityService.GetAuthenticationStateAsync();

                persistentComponentState.RegisterOnPersisting(PersistAuthenticationState, RenderMode.InteractiveAuto);
            }

            return authenticationState;
        }

        private Task PersistAuthenticationState()
        {
            if (authenticationState != null)
            {
                persistentComponentState.PersistAsJson(AuthenticationStateKey, authenticationState);
            }

            return Task.CompletedTask;
        }
    }
}