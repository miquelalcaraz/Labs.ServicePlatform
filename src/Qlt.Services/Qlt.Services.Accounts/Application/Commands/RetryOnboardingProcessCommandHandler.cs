using Microsoft.Extensions.Logging;

using Qlt.Platform;
using Qlt.Platform.Messaging.Commands;
using Qlt.Platform.Messaging.Services;
using Qlt.Services.Accounts.Contracts.IntegrationEvents;
using Qlt.Services.Accounts.Domain.Models.OnboardingAggregate;
using Qlt.Services.Accounts.Domain.Repositories;

namespace Qlt.Services.Accounts.Application.Commands;

public class RetryOnboardingProcessCommandHandler : ICommandHandler<RetryOnboardingProcessCommand, bool>
{
    private readonly IOnboardingProcessRepository _onboardingRepository;
    private readonly IEventBusOutboxService _outboxService;
    private readonly ILogger<RetryOnboardingProcessCommandHandler> _logger;

    public RetryOnboardingProcessCommandHandler(
        IOnboardingProcessRepository onboardingRepository,
        IEventBusOutboxService outboxService,
        ILogger<RetryOnboardingProcessCommandHandler> logger)
    {
        _onboardingRepository = onboardingRepository ?? throw new ArgumentNullException(nameof(onboardingRepository));
        _outboxService = outboxService ?? throw new ArgumentNullException(nameof(outboxService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<Result<bool>> Handle(RetryOnboardingProcessCommand message, CancellationToken cancellationToken)
    {
        var onboarding = await _onboardingRepository.GetAsync(message.OnboardingProcessId);

        if (onboarding is null)
            return Result.Failure<bool>(Error.NotFound.WithMessage(
                $"Onboarding process with id '{message.OnboardingProcessId}' was not found."));

        if (onboarding.Status != OnboardingStatus.Failed)
            return Result.Failure<bool>(Error.Conflict.WithMessage(
                "Retry is not allowed for the current onboarding status."));

        var failedStep = onboarding.FailedStep ?? onboarding.CurrentStep;

        _logger.LogInformation(
            "Retrying onboarding process {OnboardingProcessId} from step {FailedStep}. Reason: {Reason}",
            onboarding.Id, failedStep, message.Reason);

        onboarding.Retry();
        _onboardingRepository.Update(onboarding);

        var saved = await _onboardingRepository.UnitOfWork.SaveChangesAsync(cancellationToken);

        if (saved <= 0)
            return Result.Failure<bool>(Error.BadRequest.WithMessage("Failed to retry onboarding process."));

        await RepublishIntegrationEventForStep(onboarding, failedStep);

        _logger.LogInformation(
            "Onboarding process {OnboardingProcessId} retry initiated successfully. RetryCount: {RetryCount}",
            onboarding.Id, onboarding.RetryCount);

        return Result.Success(true);
    }

    private async Task RepublishIntegrationEventForStep(OnboardingProcess onboarding, OnboardingStep failedStep)
    {
        if (failedStep is OnboardingStep.AdminProvisioningRequested
            or OnboardingStep.OwnerCompanyCreated)
        {
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
                "Re-published CustomerAdminProvisioningRequestedIntegrationEvent for onboarding {OnboardingProcessId}",
                onboarding.Id);
        }
    }
}
