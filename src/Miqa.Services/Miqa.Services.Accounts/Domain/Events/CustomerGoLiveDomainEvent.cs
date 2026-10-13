using MediatR;

using Miqa.Domain.SeedWork;

namespace Miqa.Services.Accounts.Domain.Events;

public record CustomerGoLiveDomainEvent(
    Guid SubscriptionId,
    Guid TenantId,
    Guid AdminUserId) : IDomainEvent, INotification;
