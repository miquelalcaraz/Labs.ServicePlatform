using Qlt.Platform.Messaging.Queries;

namespace Qlt.Services.Accounts.Application.Queries;

/// <summary>
/// Retrieves a single company by its identifier.
/// </summary>
public record GetCompanyByIdQuery(Guid Id) : IQuery<CompanyDetail>;
