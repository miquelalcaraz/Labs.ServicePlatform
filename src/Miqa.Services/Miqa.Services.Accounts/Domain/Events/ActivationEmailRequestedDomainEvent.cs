using MediatR;

using Miqa.Domain.SeedWork;

namespace Miqa.Services.Accounts.Domain.Events;

public record ActivationEmailRequestedDomainEvent(
    Guid ProcessId,
    Guid UserId,
    Guid TenantId) : IDomainEvent, INotification;
