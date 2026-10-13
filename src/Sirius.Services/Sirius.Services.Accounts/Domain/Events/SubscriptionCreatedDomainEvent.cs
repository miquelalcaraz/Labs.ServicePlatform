using MediatR;

using Sirius.Domain.SeedWork;

namespace Sirius.Services.Accounts.Domain.Events;

public record SubscriptionCreatedDomainEvent(
    Guid SubscriptionId,
    Guid TenantId,
    string PlanCode) : IDomainEvent, INotification;
