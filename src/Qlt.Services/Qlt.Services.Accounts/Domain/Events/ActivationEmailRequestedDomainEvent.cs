using MediatR;

using Qlt.Domain.SeedWork;

namespace Qlt.Services.Accounts.Domain.Events;

public record ActivationEmailRequestedDomainEvent(
    Guid ProcessId,
    Guid UserId,
    Guid TenantId) : IDomainEvent, INotification;
