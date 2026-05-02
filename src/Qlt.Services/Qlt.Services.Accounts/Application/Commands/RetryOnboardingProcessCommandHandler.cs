using Microsoft.Extensions.Logging;

using Qlt.Platform;
using Qlt.Platform.Messaging.Commands;

using Qlt.Services.Accounts.Domain.Models.OnboardingAggregate;
using Qlt.Services.Accounts.Domain.Repositories;

namespace Qlt.Services.Accounts.Application.Commands;

public class RetryOnboardingProcessCommandHandler : ICommandHandler<RetryOnboardingProcessCommand, bool>
{
    private readonly IOnboardingProcessRepository _onboardingRepository;
    private readonly ILogger<RetryOnboardingProcessCommandHandler> _logger;

    public RetryOnboardingProcessCommandHandler(
        IOnboardingProcessRepository onboardingRepository,
        ILogger<RetryOnboardingProcessCommandHandler> logger)
    {
        _onboardingRepository = onboardingRepository ?? throw new ArgumentNullException(nameof(onboardingRepository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<Result<bool>> Handle(RetryOnboardingProcessCommand message, CancellationToken cancellationToken)
    {
        var onboarding = await _onboardingRepository.GetAsync(message.OnboardingProcessId);

        if (onboarding is null)
        {
            _logger.LogWarning(
                "Retry requested for onboarding {OnboardingProcessId} but it was not found.",
                message.OnboardingProcessId);
            return Result.Failure<bool>(Error.NotFound.WithMessage(
                $"Onboarding process with id '{message.OnboardingProcessId}' was not found."));
        }

        if (onboarding.Status != OnboardingStatus.Failed)
        {
            _logger.LogWarning(
                "Retry not allowed for onboarding {OnboardingProcessId}. Current status: {Status}",
                onboarding.Id,
                onboarding.Status);
            return Result.Failure<bool>(Error.Conflict.WithMessage(
                "Retry is not allowed for the current onboarding status."));
        }

        var failedStep = onboarding.FailedStep ?? onboarding.CurrentStep;

        using (_logger.BeginScope(new Dictionary<string, object?> { ["CorrelationId"] = onboarding.CorrelationId }))
        {
            _logger.LogInformation(
                "Retrying onboarding process {OnboardingProcessId} from step {FailedStep}. Reason: {Reason}",
                onboarding.Id, failedStep, message.Reason);

            onboarding.Retry();
            _onboardingRepository.Update(onboarding);

            if (failedStep is OnboardingStep.AdminProvisioningRequested
                or OnboardingStep.OwnerCompanyCreated)
            {
                onboarding.RestartAdminProvisioning();
            }

            var saved = await _onboardingRepository.UnitOfWork.SaveChangesAsync(cancellationToken);

            if (saved <= 0)
                return Result.Failure<bool>(Error.BadRequest.WithMessage("Failed to retry onboarding process."));

            _logger.LogInformation(
                "Onboarding process {OnboardingProcessId} retry initiated successfully. RetryCount: {RetryCount}",
                onboarding.Id, onboarding.RetryCount);

            return Result.Success(true);
        }
    }
}
