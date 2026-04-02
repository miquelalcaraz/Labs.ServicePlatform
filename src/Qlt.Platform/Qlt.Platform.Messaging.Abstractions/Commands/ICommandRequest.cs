namespace Qlt.Platform.Messaging.Commands;

/// <summary>
/// Non-generic base interface for all commands, enabling simple pattern matching
/// without reflection in pipeline behaviors.
/// </summary>
public interface ICommandRequest { }
