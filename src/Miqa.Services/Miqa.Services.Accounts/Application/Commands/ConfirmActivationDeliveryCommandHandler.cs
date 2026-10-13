using Microsoft.Extensions.Logging;

using Miqa.Platform;
using Miqa.Platform.Messaging.Commands;
using Miqa.Services.Accounts.Domain.Exceptions;
using Miqa.Services.Accounts.Domain.Models.CustomerActivationProcessAggregate;
using Miqa.Services.Accounts.Domain.Repositories;

namespace Miqa.Services.Accounts.Application.Commands;

/// <summary>
/// Advances the activation process to WaitingForUserActivation after Notifications confirms delivery.
/// </summary>
public class ConfirmActivationDeliveryCommandHandler
    : ICommandHandler<ConfirmActivationDeliveryCommand, bool>
{
    private readonly ICustomerActivationProcessRepository _repository;
    private readonly ILogger<ConfirmActivationDeliveryCommandHandler> _logger;

    public ConfirmActivationDeliveryCommandHandler(
        ICustomerActivationProcessRepository repository,
        ILogger<ConfirmActivationDeliveryCommandHandler> logger)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<Result<bool>> Handle(ConfirmActivationDeliveryCommand message, CancellationToken cancellationToken)
    {
        var process = await _repository.GetAsync(message.ActivationProcessId);
        if (process is null)
        {
            _logger.LogWarning(
                "Activation process {ActivationProcessId} not found.",
                message.ActivationProcessId);
            return Result.Failure<bool>(Error.NotFound.WithMessage(
                $"Activation process '{message.ActivationProcessId}' not found."));
        }

        if (process.Status == ActivationStatus.Completed)
        {
            _logger.LogInformation(
                "Activation process {ActivationProcessId} already completed. Skipping delivery confirmation.",
                message.ActivationProcessId);
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
                process.MarkActivationDelivered();
                _repository.Update(process);
                await _repository.UnitOfWork.SaveChangesAsync(cancellationToken);

                _logger.LogInformation(
                    "Activation delivery confirmed for process {ActivationProcessId}.",
                    message.ActivationProcessId);

                return Result.Success(true);
            }
            catch (AccountDomainException ex)
            {
                _logger.LogError(ex,
                    "Domain error confirming activation delivery for process {ActivationProcessId}. Marking as failed.",
                    message.ActivationProcessId);

                process.Fail("ActivationDeliveredProcessingError", ex.Message);
                _repository.Update(process);
                await _repository.UnitOfWork.SaveChangesAsync(cancellationToken);

                CustomerActivationDiagnostics.ProcessesFailed.Add(1,
                    new KeyValuePair<string, object?>("error_code", "ActivationDeliveredProcessingError"));

                return Result.Failure<bool>(Error.Conflict.WithMessage(ex.Message));
            }
        }
    }
}
