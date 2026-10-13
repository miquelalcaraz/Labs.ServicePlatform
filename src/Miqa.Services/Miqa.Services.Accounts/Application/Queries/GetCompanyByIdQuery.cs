using Miqa.Platform.Messaging.Queries;

namespace Miqa.Services.Accounts.Application.Queries;

/// <summary>
/// Retrieves a single company by its identifier.
/// </summary>
public record GetCompanyByIdQuery(Guid Id) : IQuery<CompanyDetail>;
