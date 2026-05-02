using MediatR;

using Microsoft.Extensions.Logging;

using Qlt.Contracts.Identity.IntegrationEvents;
using Qlt.Platform.Messaging.IntegrationEvents;
using Qlt.Services.Accounts.Application.Commands;

namespace Qlt.Services.Accounts.Infrastructure.IntegrationEvents.EventHandling;

/// <summary>
/// Translates the ExternalUserProvisioned integration event into application commands
/// to complete onboarding and start the customer activation process.
/// </summary>
public class ExternalUserProvisionedIntegrationEventHandler
    : IIntegrationEventHandler<ExternalUserProvisionedIntegrationEvent>
{
    private readonly IMediator _mediator;
    private readonly ILogger<ExternalUserProvisionedIntegrationEventHandler> _logger;

    public ExternalUserProvisionedIntegrationEventHandler(
        IMediator mediator,
        ILogger<ExternalUserProvisionedIntegrationEventHandler> logger)
    {
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task Handle(ExternalUserProvisionedIntegrationEvent @event)
    {
        _logger.LogInformation(
            "Received ExternalUserProvisioned event: UserId={UserId}, TenantId={TenantId}, OnboardingId={OnboardingProcessId}",
            @event.UserId,
            @event.TenantId,
            @event.OnboardingProcessId);

        var completeResult = await _mediator.Send(new CompleteOnboardingCommand(
            OnboardingProcessId: @event.OnboardingProcessId,
            UserId: @event.UserId,
            TenantId: @event.TenantId,
            CompanyId: @event.CompanyId,
            SubscriptionId: @event.SubscriptionId,
            Email: @event.Email));

        if (completeResult.IsFailure)
        {
            _logger.LogWarning(
                "CompleteOnboarding failed for OnboardingId={OnboardingProcessId}: {Error}",
                @event.OnboardingProcessId,
                completeResult.Error.Message);
            return;
        }

        var onboardingResult = completeResult.Value;

        var activationResult = await _mediator.Send(new StartCustomerActivationProcessCommand(
            subscriptionId: @event.SubscriptionId,
            tenantId: @event.TenantId,
            ownerCompanyId: @event.CompanyId,
            adminUserId: @event.UserId,
            adminMembershipId: onboardingResult.AdminMembershipId,
            companyLegalName: onboardingResult.CompanyLegalName,
            subscriptionPlanCode: onboardingResult.SubscriptionPlanCode,
            adminEmail: @event.Email,
            requestId: @event.OnboardingProcessId));

        if (activationResult.IsFailure)
            _logger.LogWarning(
                "StartCustomerActivationProcess failed for subscription {SubscriptionId}: {Error}",
                @event.SubscriptionId,
                activationResult.Error.Message);
        else
            _logger.LogInformation(
                "Customer activation process started for subscription {SubscriptionId}.",
                @event.SubscriptionId);
    }
}
