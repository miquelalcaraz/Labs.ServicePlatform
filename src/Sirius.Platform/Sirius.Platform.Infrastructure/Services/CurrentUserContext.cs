using System;

using Microsoft.AspNetCore.Http;

using Sirius.Domain.Services;

namespace Sirius.Platform.Services;

/// <summary>
/// <see cref="ICurrentUserContext"/> implementation that reads the caller's claims and tracing headers
/// from the current <see cref="HttpContext"/>. Registered by <c>AddApiServiceDefaults()</c>.
/// </summary>
public class CurrentUserContext(IHttpContextAccessor context) : ICurrentUserContext
{
    public bool IsAuthenticated
        => context.HttpContext?.User.Identity?.IsAuthenticated ?? false;

    public Guid UserId
        => Guid.TryParse(context.HttpContext?.User.FindFirst("sub")?.Value, out var userId) ? userId : Guid.Empty;

    public string? UserName
        => context.HttpContext?.User.Identity?.Name;

    public Guid? TenantId
        => Guid.TryParse(context.HttpContext?.User.FindFirst("tenantid")?.Value, out var tenantId) ? tenantId : null;

    public Guid? OrganizationId
        => Guid.TryParse(context.HttpContext?.User.FindFirst("organizationid")?.Value, out var organizationId) ? organizationId : null;

    public string? Role
        => context.HttpContext?.User.FindFirst("role")?.Value;

    public string? TraceId
        => context.HttpContext?.TraceIdentifier;

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