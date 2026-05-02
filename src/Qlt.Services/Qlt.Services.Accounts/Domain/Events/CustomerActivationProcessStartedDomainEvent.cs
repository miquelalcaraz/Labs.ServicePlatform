using MediatR;

using Qlt.Domain.SeedWork;

namespace Qlt.Services.Accounts.Domain.Events;

public record CustomerActivationProcessStartedDomainEvent(
    Guid ProcessId) : IDomainEvent, INotification;
