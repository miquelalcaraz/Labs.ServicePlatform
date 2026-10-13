using Microsoft.Extensions.Logging;

using Sirius.Platform;
using Sirius.Platform.Messaging.Commands;
using Sirius.Services.Accounts.Domain.Exceptions;
using Sirius.Services.Accounts.Domain.Models.CustomerActivationProcessAggregate;
using Sirius.Services.Accounts.Domain.Repositories;

namespace Sirius.Services.Accounts.Application.Commands;

public class RetryCustomerActivationProcessCommandHandler
    : ICommandHandler<RetryCustomerActivationProcessCommand, bool>
{
    private readonly ICustomerActivationProcessRepository _repository;
    private readonly ILogger<RetryCustomerActivationProcessCommandHandler> _logger;

    public RetryCustomerActivationProcessCommandHandler(
        ICustomerActivationProcessRepository repository,
        ILogger<RetryCustomerActivationProcessCommandHandler> logger)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<Result<bool>> Handle(RetryCustomerActivationProcessCommand message, CancellationToken cancellationToken)
    {
        var process = await _repository.GetAsync(message.ActivationProcessId);
        if (process is null)
        {
            _logger.LogWarning(
                "Retry requested for activation process {ActivationProcessId} but it was not found.",
                message.ActivationProcessId);
            return Result.Failure<bool>(Error.NotFound.WithMessage(
                $"Activation process with id '{message.ActivationProcessId}' was not found."));
        }

        if (process.Status != ActivationStatus.Failed)
        {
            _logger.LogWarning(
                "Retry not allowed for activation process {ActivationProcessId}. Current status: {Status}",
                process.Id, process.Status);
            return Result.Failure<bool>(Error.Conflict.WithMessage(
                "Retry is only allowed when the activation process is in Failed status."));
        }

        using (_logger.BeginScope(new Dictionary<string, object?>
        {
            ["CorrelationId"] = process.CorrelationId,
            ["ActivationProcessId"] = process.Id
        }))
        {
            try
            {
                process.Retry();

                if (process.CurrentStep == ActivationStep.ActivationRequested)
                    process.RestartActivationDelivery();

                _repository.Update(process);

                var saved = await _repository.UnitOfWork.SaveChangesAsync(cancellationToken);

                if (saved <= 0)
                    return Result.Failure<bool>(Error.BadRequest.WithMessage("Failed to retry activation process."));

                _logger.LogInformation(
                    "Activation process {ActivationProcessId} retry initiated. RetryCount={RetryCount}, Step={Step}",
                    process.Id, process.RetryCount, process.CurrentStep);

                return Result.Success(true);
            }
            catch (AccountDomainException ex)
            {
                _logger.LogWarning(ex,
                    "Retry domain error for activation process {ActivationProcessId}",
                    process.Id);
                return Result.Failure<bool>(Error.Conflict.WithMessage(ex.Message));
            }
        }
    }
}
