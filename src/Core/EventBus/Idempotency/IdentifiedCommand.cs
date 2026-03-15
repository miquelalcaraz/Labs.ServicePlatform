using MediatR;

using System;

namespace QltSystem.Core.EventBus.Idempotency;

/// <summary>
/// Wraps a command with a unique identifier for idempotency purposes.
/// Used to ensure that the same command is not processed multiple times if the client retries.
/// </summary>
/// <typeparam name="TCommand">The type of command being wrapped</typeparam>
/// <typeparam name="TResponse">The response type of the command</typeparam>
public class IdentifiedCommand<TCommand, TResponse> : IRequest<TResponse>
    where TCommand : IRequest<TResponse>
{
    /// <summary>
    /// The actual command to execute
    /// </summary>
    public TCommand Command { get; }
    public string IdProperty { get; } = string.Empty;
    public string CommandId { get; } = string.Empty;
    /// <summary>
    /// Unique identifier for idempotency (typically from x-requestid header)
    /// </summary>
    public Guid Id { get; }

    public IdentifiedCommand(TCommand command, Guid id, string idProperty = "Id?", string commandId = "n/a")
    {
        Command = command ?? throw new ArgumentNullException(nameof(command));
        Id = id;
        IdProperty = idProperty;
        CommandId = commandId;
    }
}
