using Microsoft.Extensions.Logging;

using Qlt.Platform.Messaging.DomainEvents;
using Qlt.Platform.Messaging.Services;
using Qlt.Services.Accounts.Contracts.IntegrationEvents;
using Qlt.Services.Accounts.Domain.Events;
using Qlt.Services.Accounts.Domain.Repositories;

namespace Qlt.Services.Accounts.Application.DomainEventHandlers;

/// <summary>
/// Handles the SubscriptionCreatedDomainEvent by publishing an integration event
/// to notify other bounded contexts about the new subscription.
/// </summary>
public class SubscriptionCreatedDomainEventHandler : IDomainEventHandler<SubscriptionCreatedDomainEvent>
{
    private readonly ILogger<SubscriptionCreatedDomainEventHandler> _logger;
    private readonly ISubscriptionRepository _subscriptionRepository;
    private readonly IEventBusOutboxService _outboxService;

    public SubscriptionCreatedDomainEventHandler(
        ILogger<SubscriptionCreatedDomainEventHandler> logger,
        ISubscriptionRepository subscriptionRepository,
        IEventBusOutboxService outboxService)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _subscriptionRepository = subscriptionRepository ?? throw new ArgumentNullException(nameof(subscriptionRepository));
        _outboxService = outboxService ?? throw new ArgumentNullException(nameof(outboxService));
    }

    public async Task Handle(SubscriptionCreatedDomainEvent notification, CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Subscription created: {SubscriptionId} for Tenant {TenantId} with Plan {PlanCode}",
            notification.SubscriptionId,
            notification.TenantId,
            notification.PlanCode);

        var subscription = await _subscriptionRepository.GetAsync(notification.SubscriptionId);

        if (subscription is not null)
        {
            var integrationEvent = new SubscriptionCreatedIntegrationEvent(
                subscription.Id,
                subscription.TenantId,
                subscription.PlanCode,
                subscription.CreatedByInternalUserId);

            await _outboxService.AddAndSaveEventAsync(integrationEvent);
        }
        else
        {
            _logger.LogWarning(
                "Subscription {SubscriptionId} not found in repository during domain event handling. Integration event not published.",
                notification.SubscriptionId);
        }
    }
}
