using MediatR;

using Sirius.Domain.SeedWork;

namespace Sirius.Services.Accounts.Domain.Events;

public record CustomerActivationProcessStartedDomainEvent(
    Guid ProcessId) : IDomainEvent, INotification;
