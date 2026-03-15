namespace QltSystem.Notifications.Models
{
    public class EmailNotification
    {
        public Guid Id { get; set; }
        public string To { get; set; } = string.Empty;
        public string Subject { get; set; } = string.Empty;
        public string Body { get; set; } = string.Empty;
        public EmailType Type { get; set; }
        public NotificationStatus Status { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? SentAt { get; set; }
    }

    public enum EmailType
    {
        WelcomeOrganization,
        SalesNotification,
        UserInvitation,
        PasswordReset
    }

    public enum NotificationStatus
    {
        Pending,
        Sent,
        Failed
    }
}
