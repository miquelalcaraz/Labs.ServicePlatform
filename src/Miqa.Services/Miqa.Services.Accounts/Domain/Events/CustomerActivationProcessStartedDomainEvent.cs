using MediatR;

using Miqa.Domain.SeedWork;

namespace Miqa.Services.Accounts.Domain.Events;

public record CustomerActivationProcessStartedDomainEvent(
    Guid ProcessId) : IDomainEvent, INotification;
