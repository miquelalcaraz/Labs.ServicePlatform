using Microsoft.Extensions.Logging;

using Qlt.Contracts.Accounts.IntegrationEvents;
using Qlt.Platform.Messaging.DomainEvents;
using Qlt.Platform.Messaging.Services;

using Qlt.Services.Accounts.Domain.Events;
using Qlt.Services.Accounts.Domain.Repositories;

namespace Qlt.Services.Accounts.Application.DomainEventHandlers;

/// <summary>
/// Publishes CustomerAdminProvisioningRequestedIntegrationEvent to the outbox
/// when admin provisioning is requested during onboarding.
/// </summary>
public class AdminProvisioningRequestedDomainEventHandler : IDomainEventHandler<AdminProvisioningRequestedDomainEvent>
{
    private readonly IOnboardingProcessRepository _repository;
    private readonly IEventBusOutboxService _outboxService;
    private readonly ILogger<AdminProvisioningRequestedDomainEventHandler> _logger;

    public AdminProvisioningRequestedDomainEventHandler(
        IOnboardingProcessRepository repository,
        IEventBusOutboxService outboxService,
        ILogger<AdminProvisioningRequestedDomainEventHandler> logger)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _outboxService = outboxService ?? throw new ArgumentNullException(nameof(outboxService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task Handle(AdminProvisioningRequestedDomainEvent notification, CancellationToken cancellationToken)
    {
        var onboarding = await _repository.GetAsync(notification.OnboardingProcessId);
        if (onboarding is null)
        {
            _logger.LogWarning(
                "Onboarding process {OnboardingProcessId} not found when publishing admin provisioning event.",
                notification.OnboardingProcessId);
            return;
        }

        await _outboxService.AddAndSaveEventAsync(
            new CustomerAdminProvisioningRequestedIntegrationEvent(
                onboarding.SubscriptionId,
                onboarding.TenantId,
                onboarding.OwnerCompanyId ?? Guid.Empty,
                onboarding.Id,
                onboarding.AdminEmail ?? string.Empty,
                onboarding.AdminFirstName ?? string.Empty,
                onboarding.AdminLastName ?? string.Empty,
                onboarding.TenantName ?? string.Empty));

        _logger.LogInformation(
            "CustomerAdminProvisioningRequestedIntegrationEvent published for onboarding {OnboardingProcessId}.",
            notification.OnboardingProcessId);
    }
}
