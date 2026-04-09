namespace Services.FunctionalTests;

class AutoAuthorizeMiddleware
{
    public const string IDENTITY_ID = "9e3163b9-1ae6-4652-9dc6-7898ab7b7a00";

    private readonly RequestDelegate _next;

    public AutoAuthorizeMiddleware(RequestDelegate rd)
    {
        _next = rd;
    }

    public async Task Invoke(HttpContext httpContext)
    {
        var role = (string?)httpContext.Request.Headers["X-Test-Role"] ?? "PlatformAdmin";

        var identity = new ClaimsIdentity("cookies");

        identity.AddClaim(new Claim("sub", IDENTITY_ID));
        identity.AddClaim(new Claim("unique_name", IDENTITY_ID));
        identity.AddClaim(new Claim(ClaimTypes.Name, IDENTITY_ID));
        identity.AddClaim(new Claim(ClaimTypes.Role, role));
        identity.AddClaim(new Claim("role", role));

        httpContext.User = new ClaimsPrincipal(identity);

        await _next.Invoke(httpContext);
    }
}
