using Microsoft.Extensions.Logging;

using Miqa.Contracts.Accounts.IntegrationEvents;
using Miqa.Platform.Messaging.DomainEvents;
using Miqa.Platform.Messaging.Services;

using Miqa.Services.Accounts.Domain.Events;
using Miqa.Services.Accounts.Domain.Repositories;

namespace Miqa.Services.Accounts.Application.DomainEventHandlers;

/// <summary>
/// Publishes ActivationEmailRequestedIntegrationEvent to the outbox when activation is requested.
/// </summary>
public class ActivationEmailRequestedDomainEventHandler : IDomainEventHandler<ActivationEmailRequestedDomainEvent>
{
    private readonly ICustomerActivationProcessRepository _repository;
    private readonly IEventBusOutboxService _outboxService;
    private readonly ILogger<ActivationEmailRequestedDomainEventHandler> _logger;

    public ActivationEmailRequestedDomainEventHandler(
        ICustomerActivationProcessRepository repository,
        IEventBusOutboxService outboxService,
        ILogger<ActivationEmailRequestedDomainEventHandler> logger)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _outboxService = outboxService ?? throw new ArgumentNullException(nameof(outboxService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task Handle(ActivationEmailRequestedDomainEvent notification, CancellationToken cancellationToken)
    {
        var process = await _repository.GetAsync(notification.ProcessId);
        if (process is null)
        {
            _logger.LogWarning(
                "Activation process {ProcessId} not found when publishing activation email event.",
                notification.ProcessId);
            return;
        }

        if (string.IsNullOrWhiteSpace(process.AdminEmail))
        {
            _logger.LogWarning(
                "Activation process {ProcessId} has no AdminEmail. Cannot publish activation email event.",
                notification.ProcessId);
            return;
        }

        await _outboxService.AddAndSaveEventAsync(new ActivationEmailRequestedIntegrationEvent(
            ActivationProcessId: process.Id,
            SubscriptionId: process.SubscriptionId,
            TenantId: process.TenantId,
            OwnerCompanyId: process.OwnerCompanyId,
            AdminUserId: process.AdminUserId,
            AdminEmail: process.AdminEmail,
            CompanyLegalName: process.CompanyLegalName,
            SubscriptionPlanCode: process.SubscriptionPlanCode,
            CorrelationId: process.CorrelationId));

        _logger.LogInformation(
            "ActivationEmailRequestedIntegrationEvent published for process {ProcessId}.",
            notification.ProcessId);
    }
}
