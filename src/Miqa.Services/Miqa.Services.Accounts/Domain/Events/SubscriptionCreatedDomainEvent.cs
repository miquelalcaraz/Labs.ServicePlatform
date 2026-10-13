using MediatR;

using Miqa.Domain.SeedWork;

namespace Miqa.Services.Accounts.Domain.Events;

public record SubscriptionCreatedDomainEvent(
    Guid SubscriptionId,
    Guid TenantId,
    string PlanCode) : IDomainEvent, INotification;
