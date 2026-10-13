using Microsoft.Extensions.Logging;

using Miqa.Platform;
using Miqa.Platform.Messaging.Commands;
using Miqa.Services.Accounts.Domain.Exceptions;
using Miqa.Services.Accounts.Domain.Models.CustomerActivationProcessAggregate;
using Miqa.Services.Accounts.Domain.Repositories;

namespace Miqa.Services.Accounts.Application.Commands;

public class ResendActivationEmailCommandHandler : ICommandHandler<ResendActivationEmailCommand, bool>
{
    private readonly ICustomerActivationProcessRepository _repository;
    private readonly ILogger<ResendActivationEmailCommandHandler> _logger;

    public ResendActivationEmailCommandHandler(
        ICustomerActivationProcessRepository repository,
        ILogger<ResendActivationEmailCommandHandler> logger)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<Result<bool>> Handle(ResendActivationEmailCommand message, CancellationToken cancellationToken)
    {
        var process = await _repository.GetAsync(message.ActivationProcessId);
        if (process is null)
        {
            _logger.LogWarning(
                "Resend requested for activation process {ActivationProcessId} but it was not found.",
                message.ActivationProcessId);
            return Result.Failure<bool>(Error.NotFound.WithMessage(
                $"Activation process with id '{message.ActivationProcessId}' was not found."));
        }

        using (_logger.BeginScope(new Dictionary<string, object?>
        {
            ["CorrelationId"] = process.CorrelationId,
            ["ActivationProcessId"] = process.Id
        }))
        {
            if (process.Status == ActivationStatus.Completed)
            {
                _logger.LogWarning(
                    "Resend not allowed for completed activation process {ActivationProcessId}.",
                    process.Id);
                return Result.Failure<bool>(Error.Conflict.WithMessage(
                    "Cannot resend activation email for a completed activation process."));
            }

            try
            {
                process.RequestActivation();
                _repository.Update(process);

                var saved = await _repository.UnitOfWork.SaveChangesAsync(cancellationToken);

                if (saved <= 0)
                    return Result.Failure<bool>(Error.BadRequest.WithMessage("Failed to resend activation email."));

                _logger.LogInformation(
                    "Activation email resend requested for process {ActivationProcessId}.",
                    process.Id);

                return Result.Success(true);
            }
            catch (AccountDomainException ex)
            {
                _logger.LogWarning(ex,
                    "Domain error during resend for activation process {ActivationProcessId}.",
                    process.Id);
                return Result.Failure<bool>(Error.Conflict.WithMessage(ex.Message));
            }
        }
    }
}
