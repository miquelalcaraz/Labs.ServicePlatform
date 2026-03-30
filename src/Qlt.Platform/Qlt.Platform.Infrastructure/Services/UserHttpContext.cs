using Microsoft.AspNetCore.Http;

using Qlt.Domain.Services;

namespace Qlt.Platform.Services;

public class UserHttpContext(IHttpContextAccessor context) : IUserHttpContext
{
    public string GetUserIdentity()
        => context.HttpContext?.User.FindFirst("sub")?.Value!;

    public string GetUserName()
        => context.HttpContext?.User.Identity?.Name!;

    public string? RequestId
    {
        get
        {
            var httpContext = context.HttpContext;
            if (httpContext == null) return null;

            if (httpContext.Request.Headers.TryGetValue("x-requestid", out var requestId))
            {
                return requestId.ToString();
            }

            return null;
        }
    }

    public string? CorrelationId
    {
        get
        {
            var httpContext = context.HttpContext;
            if (httpContext == null) return null;

            if (httpContext.Request.Headers.TryGetValue("x-correlationid", out var correlationId))
            {
                return correlationId.ToString();
            }

            return null;
        }
    }
}