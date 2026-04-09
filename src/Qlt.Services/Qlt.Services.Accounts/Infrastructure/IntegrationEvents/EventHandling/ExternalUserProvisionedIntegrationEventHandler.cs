using Microsoft.Extensions.Logging;

using Qlt.Infrastructure.Identity.Contracts.IntegrationEvents;
using Qlt.Platform.Messaging.IntegrationEvents;
using Qlt.Services.Accounts.Domain.Exceptions;
using Qlt.Services.Accounts.Domain.Models.OnboardingAggregate;
using Qlt.Services.Accounts.Domain.Models.UserMembershipAggregate;
using Qlt.Services.Accounts.Domain.Repositories;

namespace Qlt.Services.Accounts.Infrastructure.IntegrationEvents.EventHandling;

/// <summary>
/// Handles external user provisioned events from Identity service.
/// Creates the admin UserMembership and completes the onboarding process.
/// </summary>
public class ExternalUserProvisionedIntegrationEventHandler : IIntegrationEventHandler<ExternalUserProvisionedIntegrationEvent>
{
    private readonly ILogger<ExternalUserProvisionedIntegrationEventHandler> _logger;
    private readonly IUserMembershipRepository _membershipRepository;
    private readonly IOnboardingProcessRepository _onboardingRepository;

    public ExternalUserProvisionedIntegrationEventHandler(
        ILogger<ExternalUserProvisionedIntegrationEventHandler> logger,
        IUserMembershipRepository membershipRepository,
        IOnboardingProcessRepository onboardingRepository)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _membershipRepository = membershipRepository ?? throw new ArgumentNullException(nameof(membershipRepository));
        _onboardingRepository = onboardingRepository ?? throw new ArgumentNullException(nameof(onboardingRepository));
    }

    public async Task Handle(ExternalUserProvisionedIntegrationEvent @event)
    {
        _logger.LogInformation(
            "Account - Processing external user provisioned event: UserId={UserId}, TenantId={TenantId}, OnboardingId={OnboardingProcessId}",
            @event.UserId,
            @event.TenantId,
            @event.OnboardingProcessId);

        var onboarding = await _onboardingRepository.GetAsync(@event.OnboardingProcessId);
        if (onboarding is null)
        {
            _logger.LogWarning(
                "Account - OnboardingProcess {OnboardingProcessId} not found. Cannot process external user provisioned event.",
                @event.OnboardingProcessId);
            return;
        }

        if (onboarding.Status == OnboardingStatus.Completed)
        {
            _logger.LogInformation(
                "Account - OnboardingProcess {OnboardingProcessId} already completed. Ignoring duplicate event.",
                @event.OnboardingProcessId);
            return;
        }

        using (_logger.BeginScope(new Dictionary<string, object?> { ["CorrelationId"] = onboarding.CorrelationId }))
        {
            try
            {
                var membership = UserMembership.CreateExternalAdminMembership(
                    @event.UserId,
                    @event.TenantId,
                    @event.CompanyId);

                _membershipRepository.Add(membership);

                onboarding.MarkAdminUserProvisioned(@event.UserId);
                onboarding.MarkAdminMembershipCreated(membership.Id);
                onboarding.Complete();

                _onboardingRepository.Update(onboarding);

                await _membershipRepository.UnitOfWork.SaveChangesAsync();

                _logger.LogInformation(
                    "Account - UserMembership created and onboarding completed for UserId={UserId}, OnboardingId={OnboardingProcessId}",
                    @event.UserId,
                    @event.OnboardingProcessId);
            }
            catch (AccountDomainException ex)
            {
                _logger.LogError(ex,
                    "Account - Domain error completing onboarding {OnboardingProcessId} for UserId={UserId}. Marking as failed. Step={CurrentStep}",
                    @event.OnboardingProcessId,
                    @event.UserId,
                    onboarding.CurrentStep);

                onboarding.Fail("IntegrationEventProcessingError", ex.Message);
                _onboardingRepository.Update(onboarding);
                await _onboardingRepository.UnitOfWork.SaveChangesAsync();
            }
        }
    }
}
