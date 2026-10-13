using Sirius.Platform.Messaging.Queries;

namespace Sirius.Services.Accounts.Application.Queries;

public record GetCustomerActivationProcessByIdQuery(Guid Id)
    : IQuery<CustomerActivationProcessDetailResponse>;
