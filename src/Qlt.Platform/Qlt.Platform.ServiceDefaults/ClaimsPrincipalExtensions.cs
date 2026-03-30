using System.Security.Claims;

namespace Qlt.Platform;

public static class ClaimsPrincipalExtensions
{
    public static string? GetUserId(this ClaimsPrincipal principal)
        => principal.FindFirst("sub")?.Value;

    public static string? GetUserName(this ClaimsPrincipal principal) =>
        principal.FindFirst(x => x.Type == ClaimTypes.Name)?.Value;
}
