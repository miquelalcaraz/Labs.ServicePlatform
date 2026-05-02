using MediatR;

using Qlt.Domain.SeedWork;

namespace Qlt.Services.Accounts.Domain.Events;

public record CustomerGoLiveDomainEvent(
    Guid SubscriptionId,
    Guid TenantId,
    Guid AdminUserId) : IDomainEvent, INotification;
