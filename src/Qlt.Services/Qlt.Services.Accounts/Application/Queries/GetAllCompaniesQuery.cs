using Qlt.Platform.Messaging.Queries;

namespace Qlt.Services.Accounts.Application.Queries;

/// <summary>
/// Retrieves a summary list of all companies.
/// </summary>
public record GetAllCompaniesQuery : IQuery<IEnumerable<CompanySummary>>;
