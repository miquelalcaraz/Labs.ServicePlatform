using Qlt.Platform.Messaging.Queries;

namespace Qlt.Services.Accounts.Application.Queries;

/// <summary>
/// Retrieves a summary list of all organizations.
/// </summary>
public record GetAllOrganizationsQuery : IQuery<IEnumerable<OrganizationSummary>>;
