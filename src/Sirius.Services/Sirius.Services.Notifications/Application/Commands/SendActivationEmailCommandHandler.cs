using Microsoft.Extensions.Logging;

using Sirius.Contracts.Notifications.IntegrationEvents;
using Sirius.Platform;
using Sirius.Platform.Messaging.Commands;
using Sirius.Platform.Messaging.Services;
using Sirius.Services.Notifications.Domain.Models;
using Sirius.Services.Notifications.Domain.Repositories;
using Sirius.Services.Notifications.Domain.Services;

namespace Sirius.Services.Notifications.Application.Commands;

/// <summary>
/// Sends the activation email, persists the notification record, and publishes ActivationDeliveredIntegrationEvent.
/// </summary>
public class SendActivationEmailCommandHandler
    : ICommandHandler<SendActivationEmailCommand, bool>
{
    private readonly IEmailNotificationRepository _repository;
    private readonly IEmailService _emailService;
    private readonly IEventBusOutboxService _outboxService;
    private readonly ILogger<SendActivationEmailCommandHandler> _logger;

    public SendActivationEmailCommandHandler(
        IEmailNotificationRepository repository,
        IEmailService emailService,
        IEventBusOutboxService outboxService,
        ILogger<SendActivationEmailCommandHandler> logger)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _emailService = emailService ?? throw new ArgumentNullException(nameof(emailService));
        _outboxService = outboxService ?? throw new ArgumentNullException(nameof(outboxService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<Result<bool>> Handle(SendActivationEmailCommand message, CancellationToken cancellationToken)
    {
        var subject = $"Your account is now active{(message.CompanyLegalName is not null ? $" - {message.CompanyLegalName}" : string.Empty)}";
        var body = $"Your account has been activated successfully. Activation reference: {message.ActivationProcessId}\nActivation link: https://identity/Account/ConfirmEmail?userId={message.AdminUserId}";

        var notification = EmailNotification.Create(
            message.AdminEmail,
            subject,
            body,
            EmailType.ActivationEmail,
            message.CorrelationId);

        _repository.Add(notification);

        await _emailService.SendAsync(notification.To, notification.Subject, notification.Body);

        notification.MarkSent();

        await _outboxService.AddAndSaveEventAsync(
            new ActivationDeliveredIntegrationEvent(
                ActivationProcessId: message.ActivationProcessId,
                UserId: message.AdminUserId,
                Email: message.AdminEmail,
                DeliveredAtUtc: DateTimeOffset.UtcNow,
                CorrelationId: message.CorrelationId));

        _logger.LogInformation(
            "Activation email sent and ActivationDeliveredIntegrationEvent published for ActivationProcessId={ActivationProcessId}",
            message.ActivationProcessId);

        return Result.Success(true);
    }
}
