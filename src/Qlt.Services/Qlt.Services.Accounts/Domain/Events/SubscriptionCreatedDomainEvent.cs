using MediatR;

using Qlt.Domain.SeedWork;

namespace Qlt.Services.Accounts.Domain.Events;

public record SubscriptionCreatedDomainEvent(
    Guid SubscriptionId,
    Guid TenantId,
    string PlanCode) : IDomainEvent, INotification;
