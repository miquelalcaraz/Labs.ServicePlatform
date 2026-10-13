using System.Reflection;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Miqa.Infrastructure.Identity.Pages;

[AllowAnonymous]
public class Index : PageModel
{
    public string Version
    {
        get => typeof(Duende.IdentityServer.Hosting.IdentityServerMiddleware).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion.Split('+').First()
            ?? "unavailable";
    }
}
