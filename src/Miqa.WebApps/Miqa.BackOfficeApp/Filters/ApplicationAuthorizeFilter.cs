using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Authorization;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Miqa.BackOfficeApp.Filters
{
    public class ApplicationAuthorizeFilter : AuthorizeFilter
    {
        public ApplicationAuthorizeFilter(AuthorizationPolicy policy) : base(policy)
        {
        }

        public override Task OnAuthorizationAsync(AuthorizationFilterContext context)
        {
            if (context.HttpContext.Request.Path.StartsWithSegments("/auth") ||
                context.HttpContext.Request.Path.StartsWithSegments("/signin-oidc") ||
                context.HttpContext.Request.Path.StartsWithSegments("/signout-callback-oidc"))
            {
                return Task.CompletedTask;
            }

            return base.OnAuthorizationAsync(context);
        }
    }
}