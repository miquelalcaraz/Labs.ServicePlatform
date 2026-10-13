using System.Security.Claims;

using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Web;

using Miqa.BackOfficeApp.Models;

namespace Miqa.BackOfficeApp.Services
{
    public class ApplicationAuthenticationStateProvider : AuthenticationStateProvider
    {
        private const string AuthenticationStateKey = "Miqa.BackOfficeApp.AuthenticationState";
        private readonly SecurityService securityService;
        private readonly PersistentComponentState persistentComponentState;
        private readonly IHttpContextAccessor httpContextAccessor;
        private ApplicationAuthenticationState authenticationState;

        public ApplicationAuthenticationStateProvider(
            SecurityService securityService,
            PersistentComponentState persistentComponentState,
            IHttpContextAccessor httpContextAccessor)
        {
            this.securityService = securityService;
            this.persistentComponentState = persistentComponentState;
            this.httpContextAccessor = httpContextAccessor;
        }

        public override async Task<AuthenticationState> GetAuthenticationStateAsync()
        {
            var identity = new ClaimsIdentity();

            try
            {
                var state = await GetApplicationAuthenticationStateAsync();

                if (state.IsAuthenticated)
                {
                    identity = new ClaimsIdentity(state.Claims.Select(c => new Claim(c.Type, c.Value)), "Miqa.BackOfficeApp");
                }
            }
            catch (Exception ex) when (ex is HttpRequestException or InvalidOperationException)
            {
            }

            var result = new AuthenticationState(new ClaimsPrincipal(identity));

            await securityService.InitializeAsync(result);

            return result;
        }

        private async Task<ApplicationAuthenticationState> GetApplicationAuthenticationStateAsync()
        {
            try
            {
                if (persistentComponentState.TryTakeFromJson<ApplicationAuthenticationState>(AuthenticationStateKey, out var persistedState))
                {
                    authenticationState = persistedState;
                    return authenticationState;
                }
            }
            catch (InvalidOperationException)
            {
            }

            if (authenticationState != null)
            {
                return authenticationState;
            }

            var httpContext = httpContextAccessor.HttpContext;
            if (httpContext != null)
            {
                var user = httpContext.User;
                authenticationState = new ApplicationAuthenticationState
                {
                    IsAuthenticated = user.Identity?.IsAuthenticated == true,
                    Name = user.Identity?.Name,
                    Claims = user.Claims.Select(c => new ApplicationClaim { Type = c.Type, Value = c.Value })
                };

                try
                {
                    persistentComponentState.RegisterOnPersisting(PersistAuthenticationState, RenderMode.InteractiveServer);
                }
                catch (InvalidOperationException)
                {
                }

                return authenticationState;
            }

            authenticationState = await securityService.GetAuthenticationStateAsync();
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