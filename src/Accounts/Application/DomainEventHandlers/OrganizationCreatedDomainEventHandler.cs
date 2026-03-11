using Microsoft.Extensions.Logging;

using QltSystem.Accounts.Application.IntegrationEvents.Events;

using QltSystem.Accounts.Domain.Events;
using QltSystem.Accounts.Domain.Repositories;
using QltSystem.Core.Domain.Messaging;
using QltSystem.Core.EventBus.Services;

namespace QltSystem.Accounts.Application.DomainEventHandlers
{
    /// <summary>
    /// Handles the OrganizationCreatedDomainEvent by publishing an integration event
    /// to notify other bounded contexts about the new organization.
    /// Queries the repository to ensure the entity is being tracked before publishing.
    /// </summary>
    public class OrganizationCreatedDomainEventHandler : IDomainEventHandler<OrganizationCreatedDomainEvent>
    {
        private readonly ILogger<OrganizationCreatedDomainEventHandler> _logger;
        private readonly IOrganizationRepository _organizationRepository;
        private readonly IIntegrationEventService _accountIntegrationEventService;

        public OrganizationCreatedDomainEventHandler(
            ILogger<OrganizationCreatedDomainEventHandler> logger,
            IOrganizationRepository organizationRepository,
            IIntegrationEventService accountIntegrationEventService)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _organizationRepository = organizationRepository ?? throw new ArgumentNullException(nameof(organizationRepository));
            _accountIntegrationEventService = accountIntegrationEventService ?? throw new ArgumentNullException(nameof(accountIntegrationEventService));
        }

        public async Task Handle(OrganizationCreatedDomainEvent notification, CancellationToken cancellationToken)
        {
            _logger.LogInformation(
                "Organization created: {OrganizationId} - {Name}",
                notification.OrganizationId,
                notification.Name);

            // Query to verify the entity is being tracked (will be persisted)
            // This acts as a safety check before publishing integration events
            var organization = await _organizationRepository.GetAsync(notification.OrganizationId);

            if (organization != null)
            {
                _logger.LogInformation(
                    "Publishing integration event for organization: {OrganizationId} - {Name}",
                    organization.Id,
                    organization.Name);

                var integrationEvent = new OrganizationCreatedIntegrationEvent(
                    organization.TenantId,
                    organization.Id,
                    organization.Name,
                    organization.LegalName,
                    organization.TaxNumber,
                    organization.Address?.CountryCode!
                );

                await _accountIntegrationEventService.AddAndSaveEventAsync(integrationEvent);
            }
            else
            {
                _logger.LogWarning(
                    "Organization {OrganizationId} not found in repository during domain event handling. Integration event not published.",
                    notification.OrganizationId);
            }
        }
    }
}
