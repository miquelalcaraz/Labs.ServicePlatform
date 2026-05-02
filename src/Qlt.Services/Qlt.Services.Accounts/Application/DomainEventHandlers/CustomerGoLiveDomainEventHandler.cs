using Microsoft.Extensions.Logging;

using Qlt.Contracts.Accounts.IntegrationEvents;
using Qlt.Platform.Messaging.DomainEvents;
using Qlt.Platform.Messaging.Services;

using Qlt.Services.Accounts.Domain.Events;
using Qlt.Services.Accounts.Domain.Repositories;

namespace Qlt.Services.Accounts.Application.DomainEventHandlers;

/// <summary>
/// Publishes CustomerActivationCompletedIntegrationEvent and CustomerGoLiveIntegrationEvent on go-live.
/// </summary>
public class CustomerGoLiveDomainEventHandler : IDomainEventHandler<CustomerGoLiveDomainEvent>
{
    private readonly ICustomerActivationProcessRepository _repository;
    private readonly IEventBusOutboxService _outboxService;
    private readonly ILogger<CustomerGoLiveDomainEventHandler> _logger;

    public CustomerGoLiveDomainEventHandler(
        ICustomerActivationProcessRepository repository,
        IEventBusOutboxService outboxService,
        ILogger<CustomerGoLiveDomainEventHandler> logger)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _outboxService = outboxService ?? throw new ArgumentNullException(nameof(outboxService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task Handle(CustomerGoLiveDomainEvent notification, CancellationToken cancellationToken)
    {
        var process = await _repository.GetBySubscriptionIdAsync(notification.SubscriptionId);
        if (process is null)
        {
            _logger.LogWarning(
                "Activation process not found for subscription {SubscriptionId} on go-live event.",
                notification.SubscriptionId);
            return;
        }

        await _outboxService.AddAndSaveEventAsync(new CustomerActivationCompletedIntegrationEvent(
            ActivationProcessId: process.Id,
            SubscriptionId: process.SubscriptionId,
            TenantId: process.TenantId,
            OwnerCompanyId: process.OwnerCompanyId,
            AdminUserId: process.AdminUserId,
            AdminMembershipId: process.AdminMembershipId,
            CorrelationId: process.CorrelationId));

        await _outboxService.AddAndSaveEventAsync(new CustomerGoLiveIntegrationEvent(
            ActivationProcessId: process.Id,
            SubscriptionId: process.SubscriptionId,
            TenantId: process.TenantId,
            CorrelationId: process.CorrelationId));

        _logger.LogInformation(
            "CustomerActivationCompleted and CustomerGoLive integration events published for process {ProcessId}.",
            process.Id);
    }
}
