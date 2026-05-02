using Qlt.Platform.Messaging.Commands;

namespace Qlt.Infrastructure.Identity.Commands;

public record ConfirmEmailCommand(string UserId) : ICommand<ConfirmEmailResult>;

public enum ConfirmEmailStatus { Error, Success, AlreadyConfirmed }

public record ConfirmEmailResult(ConfirmEmailStatus Status, string? UserEmail);
