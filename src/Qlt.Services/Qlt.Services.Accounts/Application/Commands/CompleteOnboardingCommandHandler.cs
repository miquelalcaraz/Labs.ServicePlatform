using Microsoft.Extensions.Logging;

using Qlt.Platform;
using Qlt.Platform.Messaging.Commands;
using Qlt.Services.Accounts.Domain.Exceptions;
using Qlt.Services.Accounts.Domain.Models.OnboardingAggregate;
using Qlt.Services.Accounts.Domain.Models.UserMembershipAggregate;
using Qlt.Services.Accounts.Domain.Repositories;

namespace Qlt.Services.Accounts.Application.Commands;

/// <summary>
/// Creates the admin UserMembership and completes the onboarding process.
/// </summary>
public class CompleteOnboardingCommandHandler
    : ICommandHandler<CompleteOnboardingCommand, CompleteOnboardingResult>
{
    private readonly IOnboardingProcessRepository _onboardingRepository;
    private readonly IUserMembershipRepository _membershipRepository;
    private readonly ILogger<CompleteOnboardingCommandHandler> _logger;

    public CompleteOnboardingCommandHandler(
        IOnboardingProcessRepository onboardingRepository,
        IUserMembershipRepository membershipRepository,
        ILogger<CompleteOnboardingCommandHandler> logger)
    {
        _onboardingRepository = onboardingRepository ?? throw new ArgumentNullException(nameof(onboardingRepository));
        _membershipRepository = membershipRepository ?? throw new ArgumentNullException(nameof(membershipRepository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<Result<CompleteOnboardingResult>> Handle(CompleteOnboardingCommand message, CancellationToken cancellationToken)
    {
        var onboarding = await _onboardingRepository.GetAsync(message.OnboardingProcessId);
        if (onboarding is null)
        {
            _logger.LogWarning(
                "OnboardingProcess {OnboardingProcessId} not found.",
                message.OnboardingProcessId);
            return Result.Failure<CompleteOnboardingResult>(Error.NotFound.WithMessage(
                $"OnboardingProcess '{message.OnboardingProcessId}' not found."));
        }

        if (onboarding.Status == OnboardingStatus.Completed)
        {
            _logger.LogInformation(
                "OnboardingProcess {OnboardingProcessId} already completed.",
                message.OnboardingProcessId);
            return Result.Success(new CompleteOnboardingResult(
                onboarding.AdminMembershipId ?? Guid.Empty,
                onboarding.CompanyLegalName,
                onboarding.SubscriptionPlanCode));
        }

        using (_logger.BeginScope(new Dictionary<string, object?> { ["CorrelationId"] = onboarding.CorrelationId }))
        {
            try
            {
                var membership = UserMembership.CreateExternalAdminMembership(
                    message.UserId,
                    message.TenantId,
                    message.CompanyId);

                _membershipRepository.Add(membership);

                onboarding.MarkAdminUserProvisioned(message.UserId);
                onboarding.MarkAdminMembershipCreated(membership.Id);
                onboarding.Complete();

                _onboardingRepository.Update(onboarding);
                await _membershipRepository.UnitOfWork.SaveChangesAsync(cancellationToken);

                _logger.LogInformation(
                    "UserMembership created and onboarding completed for UserId={UserId}, OnboardingId={OnboardingProcessId}",
                    message.UserId,
                    message.OnboardingProcessId);

                return Result.Success(new CompleteOnboardingResult(
                    membership.Id,
                    onboarding.CompanyLegalName,
                    onboarding.SubscriptionPlanCode));
            }
            catch (AccountDomainException ex)
            {
                _logger.LogError(ex,
                    "Domain error completing onboarding {OnboardingProcessId} for UserId={UserId}. Marking as failed.",
                    message.OnboardingProcessId,
                    message.UserId);

                onboarding.Fail("IntegrationEventProcessingError", ex.Message);
                _onboardingRepository.Update(onboarding);
                await _onboardingRepository.UnitOfWork.SaveChangesAsync(cancellationToken);

                return Result.Failure<CompleteOnboardingResult>(Error.Conflict.WithMessage(ex.Message));
            }
        }
    }
}
