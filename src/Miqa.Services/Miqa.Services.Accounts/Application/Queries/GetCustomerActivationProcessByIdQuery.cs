using Miqa.Platform.Messaging.Queries;

namespace Miqa.Services.Accounts.Application.Queries;

public record GetCustomerActivationProcessByIdQuery(Guid Id)
    : IQuery<CustomerActivationProcessDetailResponse>;
