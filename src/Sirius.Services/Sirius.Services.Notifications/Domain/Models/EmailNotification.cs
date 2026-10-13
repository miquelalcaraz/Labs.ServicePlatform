using Sirius.Domain.SeedWork;

namespace Sirius.Services.Notifications.Domain.Models;

public class EmailNotification : Entity, IAggregateRoot
{
    public string To { get; private set; } = string.Empty;
    public string Subject { get; private set; } = string.Empty;
    public string Body { get; private set; } = string.Empty;
    public EmailType Type { get; private set; }
    public NotificationStatus Status { get; private set; }
    public DateTimeOffset? SentAtUtc { get; private set; }
    public string? ErrorMessage { get; private set; }
    public Guid? CorrelationId { get; private set; }

    protected EmailNotification() { }

    public static EmailNotification Create(
        string to,
        string subject,
        string body,
        EmailType type,
        Guid? correlationId = null)
    {
        if (string.IsNullOrWhiteSpace(to))
            throw new ArgumentException("Recipient email is required.", nameof(to));

        if (string.IsNullOrWhiteSpace(subject))
            throw new ArgumentException("Subject is required.", nameof(subject));

        return new EmailNotification
        {
            To = to,
            Subject = subject,
            Body = body,
            Type = type,
            Status = NotificationStatus.Pending,
            CorrelationId = correlationId
        };
    }

    public void MarkSent()
    {
        Status = NotificationStatus.Sent;
        SentAtUtc = DateTimeOffset.UtcNow;
    }

    public void MarkFailed(string errorMessage)
    {
        Status = NotificationStatus.Failed;
        ErrorMessage = errorMessage;
    }
}

public enum EmailType
{
    WelcomeOrganization,
    SalesNotification,
    UserInvitation,
    PasswordReset,
    ActivationEmail
}

public enum NotificationStatus
{
    Pending,
    Sent,
    Failed
}
