using MediatR;

using Sirius.Domain.SeedWork;

namespace Sirius.Services.Accounts.Domain.Events;

public record AdminProvisioningRequestedDomainEvent(
    Guid OnboardingProcessId) : IDomainEvent, INotification;
