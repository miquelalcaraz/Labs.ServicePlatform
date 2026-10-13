using Sirius.Platform.Messaging.Queries;

namespace Sirius.Services.Accounts.Application.Queries;

/// <summary>
/// Retrieves a summary list of all companies.
/// </summary>
public record GetAllCompaniesQuery : IQuery<IEnumerable<CompanySummary>>;
