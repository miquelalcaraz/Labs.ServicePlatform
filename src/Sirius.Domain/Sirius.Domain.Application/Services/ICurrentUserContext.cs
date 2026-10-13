namespace Sirius.Domain.Services;

/// <summary>
/// Represents the caller of the current request: authenticated identity and tenancy scope,
/// plus request-tracing metadata. Identity fields must be resolved once at the API/Blazor
/// boundary and passed explicitly into commands/queries — never injected into an Application-layer
/// command handler, domain event handler, integration event handler, or background worker, since
/// those run outside an HTTP request and <see cref="IsAuthenticated"/> would be false there.
/// </summary>
public interface ICurrentUserContext
{
    /// <summary>
    /// True when the current scope has an authenticated HTTP user. False for background/worker
    /// scopes (watchdogs, integration event consumers) where there is no HTTP request at all.
    /// </summary>
    bool IsAuthenticated { get; }

    /// <summary>
    /// The authenticated user id ("sub" claim). <see cref="Guid.Empty"/> when not authenticated.
    /// </summary>
    Guid UserId { get; }

    /// <summary>
    /// The authenticated user's display name.
    /// </summary>
    string? UserName { get; }

    /// <summary>
    /// The tenant scope ("tenantid" claim), when present.
    /// </summary>
    Guid? TenantId { get; }

    /// <summary>
    /// The organization scope ("organizationid" claim), when present.
    /// </summary>
    Guid? OrganizationId { get; }

    /// <summary>
    /// The normalized caller role ("role" claim).
    /// </summary>
    string? Role { get; }

    /// <summary>
    /// The membership scope ("scopecode" claim: "Tenant" or "Company"). Default-implemented as
    /// <see langword="null"/> so existing implementers (Web API adapter, test doubles) do not need
    /// changes — currently only meaningful to Blazor frontend consumers.
    /// </summary>
    string? ScopeCode => null;

    /// <summary>
    /// Gets the unique identifier for the current request.
    /// Used for idempotency and request tracking.
    /// </summary>
    string? RequestId { get; }

    /// <summary>
    /// Gets the correlation identifier that spans multiple requests/operations.
    /// Used for distributed tracing across services.
    /// </summary>
    string? CorrelationId { get; }

    /// <summary>
    /// Gets the trace identifier for the current request. Degrades to null outside an HTTP request.
    /// </summary>
    string? TraceId { get; }
}