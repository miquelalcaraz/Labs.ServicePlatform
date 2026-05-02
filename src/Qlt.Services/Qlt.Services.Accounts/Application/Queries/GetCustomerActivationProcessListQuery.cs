using Qlt.Platform.Messaging.Queries;

namespace Qlt.Services.Accounts.Application.Queries;

public record GetCustomerActivationProcessListQuery(
    string? Status,
    string? CompanyLegalName,
    string? AdminEmail,
    int Page = 1,
    int PageSize = 20,
    string SortBy = "startedAtUtc",
    string SortDirection = "desc") : IQuery<CustomerActivationProcessListResponse>;
