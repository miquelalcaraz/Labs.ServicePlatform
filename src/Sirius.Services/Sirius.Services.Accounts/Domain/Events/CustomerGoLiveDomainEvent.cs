using MediatR;

using Sirius.Domain.SeedWork;

namespace Sirius.Services.Accounts.Domain.Events;

public record CustomerGoLiveDomainEvent(
    Guid SubscriptionId,
    Guid TenantId,
    Guid AdminUserId) : IDomainEvent, INotification;
