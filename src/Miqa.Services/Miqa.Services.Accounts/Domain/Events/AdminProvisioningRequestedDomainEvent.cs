using MediatR;

using Miqa.Domain.SeedWork;

namespace Miqa.Services.Accounts.Domain.Events;

public record AdminProvisioningRequestedDomainEvent(
    Guid OnboardingProcessId) : IDomainEvent, INotification;
