using Qlt.Platform.Messaging.Queries;

namespace Qlt.Services.Accounts.Application.Queries;

/// <summary>
/// Retrieves a single organization by its identifier.
/// </summary>
public record GetOrganizationByIdQuery(Guid Id) : IQuery<Organization>;
