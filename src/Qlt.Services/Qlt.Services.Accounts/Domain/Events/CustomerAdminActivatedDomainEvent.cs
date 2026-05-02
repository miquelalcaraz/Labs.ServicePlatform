using MediatR;

using Qlt.Domain.SeedWork;

namespace Qlt.Services.Accounts.Domain.Events;

public record CustomerAdminActivatedDomainEvent(
    Guid ProcessId,
    Guid UserId) : IDomainEvent, INotification;
