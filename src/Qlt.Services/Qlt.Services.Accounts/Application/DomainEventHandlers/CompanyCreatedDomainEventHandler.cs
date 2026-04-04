using Microsoft.Extensions.Logging;

using Qlt.Platform.Messaging.DomainEvents;
using Qlt.Platform.Messaging.Services;
using Qlt.Services.Accounts.Contracts.IntegrationEvents;
using Qlt.Services.Accounts.Domain.Events;
using Qlt.Services.Accounts.Domain.Repositories;

namespace Qlt.Services.Accounts.Application.DomainEventHandlers;

/// <summary>
/// Handles the CompanyCreatedDomainEvent by publishing an integration event
/// to notify other bounded contexts about the new company.
/// </summary>
public class CompanyCreatedDomainEventHandler : IDomainEventHandler<CompanyCreatedDomainEvent>
{
    private readonly ILogger<CompanyCreatedDomainEventHandler> _logger;
    private readonly ICompanyRepository _companyRepository;
    private readonly IEventBusOutboxService _outboxService;

    public CompanyCreatedDomainEventHandler(
        ILogger<CompanyCreatedDomainEventHandler> logger,
        ICompanyRepository companyRepository,
        IEventBusOutboxService outboxService)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _companyRepository = companyRepository ?? throw new ArgumentNullException(nameof(companyRepository));
        _outboxService = outboxService ?? throw new ArgumentNullException(nameof(outboxService));
    }

    public async Task Handle(CompanyCreatedDomainEvent notification, CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Company created: {CompanyId} - {LegalName}",
            notification.CompanyId,
            notification.LegalName);

        var company = await _companyRepository.GetAsync(notification.CompanyId);

        if (company != null)
        {
            _logger.LogInformation(
                "Publishing integration event for company: {CompanyId} - {LegalName}",
                company.Id,
                company.LegalName);

            var integrationEvent = new CompanyCreatedIntegrationEvent(
                company.TenantId,
                company.Id,
                company.LegalName,
                company.TradeName,
                company.TaxId,
                company.Address?.CountryCode!);

            await _outboxService.AddAndSaveEventAsync(integrationEvent);
        }
        else
        {
            _logger.LogWarning(
                "Company {CompanyId} not found in repository during domain event handling. Integration event not published.",
                notification.CompanyId);
        }
    }
}
