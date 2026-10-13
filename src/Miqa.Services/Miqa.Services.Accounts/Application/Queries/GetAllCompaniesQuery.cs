using Miqa.Platform.Messaging.Queries;

namespace Miqa.Services.Accounts.Application.Queries;

/// <summary>
/// Retrieves a summary list of all companies.
/// </summary>
public record GetAllCompaniesQuery : IQuery<IEnumerable<CompanySummary>>;
