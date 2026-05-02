using MediatR;

using Qlt.Domain.SeedWork;

namespace Qlt.Services.Accounts.Domain.Events;

public record AdminProvisioningRequestedDomainEvent(
    Guid OnboardingProcessId) : IDomainEvent, INotification;
