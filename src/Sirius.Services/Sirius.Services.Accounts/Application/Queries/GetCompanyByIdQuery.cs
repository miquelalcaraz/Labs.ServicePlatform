using Sirius.Platform.Messaging.Queries;

namespace Sirius.Services.Accounts.Application.Queries;

/// <summary>
/// Retrieves a single company by its identifier.
/// </summary>
public record GetCompanyByIdQuery(Guid Id) : IQuery<CompanyDetail>;
