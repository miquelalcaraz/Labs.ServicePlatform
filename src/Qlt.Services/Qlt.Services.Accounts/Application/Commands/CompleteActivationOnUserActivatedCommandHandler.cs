using Microsoft.Extensions.Logging;

using Qlt.Platform;
using Qlt.Platform.Messaging.Commands;
using Qlt.Services.Accounts.Domain.Exceptions;
using Qlt.Services.Accounts.Domain.Models.CustomerActivationProcessAggregate;
using Qlt.Services.Accounts.Domain.Repositories;

namespace Qlt.Services.Accounts.Application.Commands;

/// <summary>
/// Completes the activation process when Identity confirms the user has activated their account.
/// </summary>
public class CompleteActivationOnUserActivatedCommandHandler
    : ICommandHandler<CompleteActivationOnUserActivatedCommand, bool>
{
    private readonly ICustomerActivationProcessRepository _repository;
    private readonly ILogger<CompleteActivationOnUserActivatedCommandHandler> _logger;

    public CompleteActivationOnUserActivatedCommandHandler(
        ICustomerActivationProcessRepository repository,
        ILogger<CompleteActivationOnUserActivatedCommandHandler> logger)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<Result<bool>> Handle(CompleteActivationOnUserActivatedCommand message, CancellationToken cancellationToken)
    {
        var process = await _repository.GetByAdminUserIdAsync(message.UserId);
        if (process is null)
        {
            _logger.LogWarning(
                "No activation process found for admin user {UserId}.",
                message.UserId);
            return Result.Failure<bool>(Error.NotFound.WithMessage(
                $"No activation process found for admin user '{message.UserId}'."));
        }

        if (process.Status == ActivationStatus.Completed)
        {
            _logger.LogInformation(
                "Activation process {ActivationProcessId} already completed. Skipping user activation.",
                process.Id);
            return Result.Success(true);
        }

        using (_logger.BeginScope(new Dictionary<string, object?>
        {
            ["CorrelationId"] = process.CorrelationId,
            ["ActivationProcessId"] = process.Id
        }))
        {
            try
            {
                process.MarkUserActivated(message.UserId);
                process.MarkCustomerReady();
                process.Complete();

                _repository.Update(process);
                await _repository.UnitOfWork.SaveChangesAsync(cancellationToken);

                _logger.LogInformation(
                    "Activation process {ActivationProcessId} completed on user activation. UserId={UserId}",
                    process.Id, message.UserId);

                CustomerActivationDiagnostics.ProcessesCompleted.Add(1);

                return Result.Success(true);
            }
            catch (AccountDomainException ex)
            {
                _logger.LogError(ex,
                    "Domain error completing activation for process {ActivationProcessId}. Marking as failed.",
                    process.Id);

                process.Fail("UserActivatedProcessingError", ex.Message);
                _repository.Update(process);
                await _repository.UnitOfWork.SaveChangesAsync(cancellationToken);

                CustomerActivationDiagnostics.ProcessesFailed.Add(1,
                    new KeyValuePair<string, object?>("error_code", "UserActivatedProcessingError"));

                return Result.Failure<bool>(Error.Conflict.WithMessage(ex.Message));
            }
        }
    }
}
