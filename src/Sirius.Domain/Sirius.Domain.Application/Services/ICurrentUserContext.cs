namespace Sirius.Domain.Services;

/// <summary>
/// Represents the caller of the current request: authenticated identity and tenancy scope,
/// plus request-tracing metadata. Every member degrades to its empty value outside an HTTP request.
/// Identity fields must be resolved at the API boundary and passed explicitly into commands/queries;
/// handlers, event consumers and background workers must not read them, since they may run without
/// an HTTP request. Request-tracing members (<see cref="RequestId"/>, <see cref="CorrelationId"/>,
/// <see cref="TraceId"/>) may be read by cross-cutting pipeline behaviors.
/// </summary>
public interface ICurrentUserContext
{
    /// <summary>
    /// True when the current scope has an authenticated HTTP user; false when unauthenticated
    /// or outside an HTTP request.
    /// </summary>
    bool IsAuthenticated { get; }

    /// <summary>
    /// The authenticated user id ("sub" claim). <see cref="Guid.Empty"/> when the claim is missing or not a valid GUID.
    /// </summary>
    Guid UserId { get; }

    /// <summary>
    /// The authenticated user's name (<c>Identity.Name</c>), or <see langword="null"/> when unavailable.
    /// </summary>
    string? UserName { get; }

    /// <summary>
    /// The tenant scope ("tenantid" claim), or <see langword="null"/> when missing or not a valid GUID.
    /// </summary>
    Guid? TenantId { get; }

    /// <summary>
    /// The organization scope ("organizationid" claim), or <see langword="null"/> when missing or not a valid GUID.
    /// </summary>
    Guid? OrganizationId { get; }

    /// <summary>
    /// The caller role ("role" claim), or <see langword="null"/> when missing.
    /// </summary>
    string? Role { get; }

    /// <summary>
    /// The membership scope ("Tenant" or "Company"). Default-implemented as <see langword="null"/>;
    /// the Web API implementation does not populate it.
    /// </summary>
    string? ScopeCode => null;

    /// <summary>
    /// The unique identifier of the current request ("x-requestid" header), used for idempotency and request tracking.
    /// </summary>
    string? RequestId { get; }

    /// <summary>
    /// The correlation identifier spanning multiple requests/operations ("x-correlationid" header), used for distributed tracing.
    /// </summary>
    string? CorrelationId { get; }

    /// <summary>
    /// The trace identifier of the current HTTP request (<c>HttpContext.TraceIdentifier</c>).
    /// </summary>
    string? TraceId { get; }
}
