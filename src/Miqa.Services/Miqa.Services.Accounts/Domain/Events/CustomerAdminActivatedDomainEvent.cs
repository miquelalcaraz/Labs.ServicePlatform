using MediatR;

using Miqa.Domain.SeedWork;

namespace Miqa.Services.Accounts.Domain.Events;

public record CustomerAdminActivatedDomainEvent(
    Guid ProcessId,
    Guid UserId) : IDomainEvent, INotification;
