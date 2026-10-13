using MediatR;

using Sirius.Domain.SeedWork;

namespace Sirius.Services.Accounts.Domain.Events;

public record CustomerAdminActivatedDomainEvent(
    Guid ProcessId,
    Guid UserId) : IDomainEvent, INotification;
