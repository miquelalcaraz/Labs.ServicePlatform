using MediatR;

using Microsoft.Extensions.Logging;

using Sirius.Domain.Services;




namespace Sirius.Platform.Apis;


/// <summary>
/// Base class for the per-service dependency bundle injected into minimal API endpoints via <c>[AsParameters]</c>.
/// </summary>
/// <typeparam name="TSelf">The derived services class, used as the logger category.</typeparam>
public abstract class ApiServices<TSelf>(
    IMediator mediator,
    ICurrentUserContext currentUser,
    ILogger<TSelf> logger)
{
    /// <summary>
    /// The mediator used to send commands and queries.
    /// </summary>
    public IMediator Mediator { get; set; } = mediator;

    /// <summary>
    /// The logger categorized by the derived services class.
    /// </summary>
    public ILogger<TSelf> Logger { get; } = logger;

    /// <summary>
    /// The caller of the current request and its tracing metadata.
    /// </summary>
    public ICurrentUserContext CurrentUser { get; } = currentUser;
}
