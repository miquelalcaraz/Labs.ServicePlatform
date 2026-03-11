using System;
using System.Threading;
using System.Threading.Tasks;

using MediatR;

using Microsoft.Extensions.Logging;

using QltSystem.Core.EventBus.Extensions;

namespace QltSystem.Core.EventBus.Idempotency;

/// <summary>
/// Base implementation for handling identified commands with idempotency support.
/// Ensures that duplicate requests (identified by the same request ID) are not processed multiple times.
/// </summary>
/// <typeparam name="TCommand">Type of the command being handled</typeparam>
/// <typeparam name="TResponse">Return type of the command</typeparam>
public abstract class IdentifiedCommandHandler<TCommand, TResponse> : IRequestHandler<IdentifiedCommand<TCommand, TResponse>, TResponse>
    where TCommand : IRequest<TResponse>
{
    private readonly IMediator _mediator;
    private readonly IRequestManager _requestManager;
    private readonly ILogger<IdentifiedCommandHandler<TCommand, TResponse>> _logger;

    protected IdentifiedCommandHandler(
        IMediator mediator,
        IRequestManager requestManager,
        ILogger<IdentifiedCommandHandler<TCommand, TResponse>> logger)
    {
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
        _requestManager = requestManager ?? throw new ArgumentNullException(nameof(requestManager));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Creates the result value to return if a previous request was found.
    /// Override this method to provide the appropriate response for duplicate requests.
    /// </summary>
    /// <returns>The result to return for duplicate requests</returns>
    protected abstract TResponse CreateResultForDuplicateRequest();

    /// <summary>
    /// Handles the identified command with idempotency.
    /// Checks if the request has already been processed, and if not, processes it.
    /// </summary>
    /// <param name="message">The identified command containing the actual command and request ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The command result or the duplicate request result</returns>
    public async Task<TResponse> Handle(IdentifiedCommand<TCommand, TResponse> message, CancellationToken cancellationToken)
    {
        var alreadyExists = await _requestManager.ExistAsync(message.Id);
        var command = message.Command;
        var commandName = command.GetGenericTypeName();

        if (alreadyExists)
        {
            _logger.LogInformation(
                "Duplicate request detected for {CommandName} with RequestId {RequestId}. Returning cached result.",
                commandName,
                message.Id);

            return CreateResultForDuplicateRequest();
        }

        // Mark request as being processed
        await _requestManager.CreateRequestForCommandAsync<TCommand>(message.Id);

        try
        {

            _logger.LogInformation(
                "Processing command {CommandName} with RequestId {RequestId}",
                commandName,
                message.Id);

            // Send the embedded business command to mediator
            var result = await _mediator.Send(command, cancellationToken);

            _logger.LogInformation(
                "Successfully processed command {CommandName} with RequestId {RequestId}",
                commandName,
                message.Id);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to process command {CommandName} with RequestId {RequestId}",
                typeof(TCommand).Name,
                message.Id);
            throw;
        }
    }
}
