using Microsoft.Extensions.Logging;

using Qlt.Platform;
using Qlt.Platform.Messaging.Commands;
using Qlt.Platform.Messaging.Services;
using Qlt.Services.Accounts.Contracts.IntegrationEvents;
using Qlt.Services.Accounts.Domain.Models.CompanyAggregate;
using Qlt.Services.Accounts.Domain.Models.OnboardingAggregate;
using Qlt.Services.Accounts.Domain.Models.SubscriptionAggregate;
using Qlt.Services.Accounts.Domain.Models.TenantAggregate;
using Qlt.Services.Accounts.Domain.Repositories;

namespace Qlt.Services.Accounts.Application.Commands;

public class StartCustomerOnboardingCommandHandler : ICommandHandler<StartCustomerOnboardingCommand, bool>
{
    private readonly ITenantRepository _tenantRepository;
    private readonly ISubscriptionRepository _subscriptionRepository;
    private readonly ICompanyRepository _companyRepository;
    private readonly IOnboardingProcessRepository _onboardingRepository;
    private readonly IEventBusOutboxService _outboxService;
    private readonly ILogger<StartCustomerOnboardingCommandHandler> _logger;

    public StartCustomerOnboardingCommandHandler(
        ITenantRepository tenantRepository,
        ISubscriptionRepository subscriptionRepository,
        ICompanyRepository companyRepository,
        IOnboardingProcessRepository onboardingRepository,
        IEventBusOutboxService outboxService,
        ILogger<StartCustomerOnboardingCommandHandler> logger)
    {
        _tenantRepository = tenantRepository ?? throw new ArgumentNullException(nameof(tenantRepository));
        _subscriptionRepository = subscriptionRepository ?? throw new ArgumentNullException(nameof(subscriptionRepository));
        _companyRepository = companyRepository ?? throw new ArgumentNullException(nameof(companyRepository));
        _onboardingRepository = onboardingRepository ?? throw new ArgumentNullException(nameof(onboardingRepository));
        _outboxService = outboxService ?? throw new ArgumentNullException(nameof(outboxService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<Result<bool>> Handle(StartCustomerOnboardingCommand message, CancellationToken cancellationToken)
    {
        using (_logger.BeginScope(new Dictionary<string, object> { ["CorrelationId"] = message.RequestId }))
        {
        var tenantName = message.TenantName ?? message.CompanyLegalName;

        var tenant = Tenant.Create(tenantName, message.CreatedByInternalUserId);
        _tenantRepository.Add(tenant);

        var subscription = Subscription.Create(tenant.Id, message.PlanCode, message.CreatedByInternalUserId);
        _subscriptionRepository.Add(subscription);

        var address = Address.Create(
            message.Country, message.CountryCode, message.State,
            message.PostalCode, message.City!, message.Street);

        var company = Company.CreateOwnerCompany(
            tenant.Id, message.CompanyLegalName, message.CompanyTradeName,
            address, message.TaxId, message.TaxNumberType, message.CreatedByInternalUserId);
        _companyRepository.Add(company);

        tenant.SetOwnerCompany(company.Id);
        subscription.SetOwnerCompany(company.Id);

        var onboarding = OnboardingProcess.Create(
            subscription.Id,
            tenant.Id,
            message.CreatedByInternalUserId,
            companyLegalName: message.CompanyLegalName,
            subscriptionPlanCode: message.PlanCode,
            adminEmail: message.AdminEmail,
            adminFirstName: message.AdminFirstName,
            adminLastName: message.AdminLastName,
            tenantName: tenantName,
            correlationId: message.RequestId);
        onboarding.Start();
        onboarding.MarkSubscriptionCreated();
        onboarding.MarkTenantCreated();
        onboarding.MarkOwnerCompanyCreated(company.Id);
        onboarding.MarkAdminProvisioningRequested();
        _onboardingRepository.Add(onboarding);

        _logger.LogInformation(
            "Starting customer onboarding: Tenant={TenantId}, Subscription={SubscriptionId}, Company={CompanyId}, Onboarding={OnboardingId}",
            tenant.Id, subscription.Id, company.Id, onboarding.Id);

        var saved = await _tenantRepository.UnitOfWork.SaveChangesAsync(cancellationToken);

        if (saved <= 0)
            return Result.Failure<bool>(Error.BadRequest.WithMessage("Failed to start customer onboarding."));

        await _outboxService.AddAndSaveEventAsync(
            new CustomerAdminProvisioningRequestedIntegrationEvent(
                subscription.Id,
                tenant.Id,
                company.Id,
                onboarding.Id,
                message.AdminEmail,
                message.AdminFirstName,
                message.AdminLastName,
                tenantName));

        _logger.LogInformation(
            "Customer onboarding started successfully. Published admin provisioning request for {AdminEmail}",
            message.AdminEmail);

        return Result.Success(true);
        }
    }
}
