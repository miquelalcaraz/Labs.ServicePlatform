using Qlt.Platform.Messaging.Queries;

namespace Qlt.Services.Accounts.Application.Queries;

public record GetCustomerActivationProcessByIdQuery(Guid Id)
    : IQuery<CustomerActivationProcessDetailResponse>;
