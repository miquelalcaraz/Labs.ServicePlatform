using MediatR;

using Sirius.Domain.SeedWork;

namespace Sirius.Services.Accounts.Domain.Events;

public record ActivationEmailRequestedDomainEvent(
    Guid ProcessId,
    Guid UserId,
    Guid TenantId) : IDomainEvent, INotification;
