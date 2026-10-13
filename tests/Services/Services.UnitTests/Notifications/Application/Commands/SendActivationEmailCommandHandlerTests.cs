using Microsoft.Extensions.Logging;

using Miqa.Contracts.Notifications.IntegrationEvents;
using Miqa.Domain.SeedWork;
using Miqa.Platform.Messaging.Services;
using Miqa.Services.Notifications.Application.Commands;
using Miqa.Services.Notifications.Domain.Models;
using Miqa.Services.Notifications.Domain.Repositories;
using Miqa.Services.Notifications.Domain.Services;

namespace Services.UnitTests.Notifications.Application.Commands;

public class SendActivationEmailCommandHandlerTests
{
    private static readonly Guid ActivationProcessId = Guid.NewGuid();
    private static readonly Guid AdminUserId = Guid.NewGuid();
    private static readonly Guid CorrelationId = Guid.NewGuid();
    private const string AdminEmail = "admin@acme.com";
    private const string CompanyName = "Acme Corp S.L.";

    [Fact]
    public async Task Handle_PersistsEmailNotificationAndPublishesDeliveredEvent()
    {
        var (repository, emailService, outbox) = CreateMocks();
        var handler = CreateHandler(repository, emailService, outbox);

        var result = await handler.Handle(CreateCommand(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();

        repository.Verify(r => r.Add(It.Is<EmailNotification>(n =>
            n.To == AdminEmail &&
            n.Type == EmailType.ActivationEmail &&
            n.Status == NotificationStatus.Sent &&
            n.CorrelationId == CorrelationId)),
            Times.Once);

        emailService.Verify(s => s.SendAsync(
            AdminEmail, It.IsAny<string>(), It.IsAny<string>()),
            Times.Once);

        outbox.Verify(o => o.AddAndSaveEventAsync(
            It.Is<ActivationDeliveredIntegrationEvent>(e =>
                e.ActivationProcessId == ActivationProcessId &&
                e.UserId == AdminUserId &&
                e.Email == AdminEmail &&
                e.CorrelationId == CorrelationId)),
            Times.Once);
    }

    [Fact]
    public async Task Handle_WithNullCompanyName_SubjectDoesNotIncludeCompanyName()
    {
        var (repository, emailService, outbox) = CreateMocks();
        var handler = CreateHandler(repository, emailService, outbox);
        var command = new SendActivationEmailCommand(
            ActivationProcessId, AdminUserId, AdminEmail, null, CorrelationId);

        await handler.Handle(command, CancellationToken.None);

        repository.Verify(r => r.Add(It.Is<EmailNotification>(n =>
            n.Subject == "Your account is now active")),
            Times.Once);
    }

    [Fact]
    public async Task Handle_WithCompanyName_SubjectIncludesCompanyName()
    {
        var (repository, emailService, outbox) = CreateMocks();
        var handler = CreateHandler(repository, emailService, outbox);

        await handler.Handle(CreateCommand(), CancellationToken.None);

        repository.Verify(r => r.Add(It.Is<EmailNotification>(n =>
            n.Subject == $"Your account is now active - {CompanyName}")),
            Times.Once);
    }

    [Fact]
    public async Task Handle_WhenEmailServiceFails_ExceptionPropagates()
    {
        var (repository, emailService, outbox) = CreateMocks();
        emailService
            .Setup(s => s.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ThrowsAsync(new InvalidOperationException("SMTP failure"));

        var handler = CreateHandler(repository, emailService, outbox);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => handler.Handle(CreateCommand(), CancellationToken.None));

        outbox.Verify(o => o.AddAndSaveEventAsync(It.IsAny<ActivationDeliveredIntegrationEvent>()), Times.Never);
    }

    [Fact]
    public async Task Handle_DeliveredEventContainsDeliveredAtUtcTimestamp()
    {
        var before = DateTimeOffset.UtcNow;
        var (repository, emailService, outbox) = CreateMocks();
        DateTimeOffset? capturedDeliveredAt = null;

        outbox
            .Setup(o => o.AddAndSaveEventAsync(It.IsAny<ActivationDeliveredIntegrationEvent>()))
            .Callback<Miqa.Platform.Messaging.IntegrationEvents.IntegrationEvent>(e =>
                capturedDeliveredAt = ((ActivationDeliveredIntegrationEvent)e).DeliveredAtUtc)
            .Returns(Task.CompletedTask);

        var handler = CreateHandler(repository, emailService, outbox);
        await handler.Handle(CreateCommand(), CancellationToken.None);

        var after = DateTimeOffset.UtcNow;
        capturedDeliveredAt.Should().NotBeNull();
        capturedDeliveredAt!.Value.Should().BeOnOrAfter(before).And.BeOnOrBefore(after);
    }

    private static SendActivationEmailCommand CreateCommand() =>
        new(ActivationProcessId, AdminUserId, AdminEmail, CompanyName, CorrelationId);

    private static (Mock<IEmailNotificationRepository> Repository, Mock<IEmailService> EmailService, Mock<IEventBusOutboxService> Outbox) CreateMocks()
    {
        var repository = new Mock<IEmailNotificationRepository>();
        repository.Setup(r => r.Add(It.IsAny<EmailNotification>()))
            .Returns<EmailNotification>(n => n);

        var unitOfWork = new Mock<IUnitOfWork>();
        unitOfWork.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
        repository.Setup(r => r.UnitOfWork).Returns(unitOfWork.Object);

        var emailService = new Mock<IEmailService>();
        emailService.Setup(s => s.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        var outbox = new Mock<IEventBusOutboxService>();
        outbox.Setup(o => o.AddAndSaveEventAsync(It.IsAny<ActivationDeliveredIntegrationEvent>()))
            .Returns(Task.CompletedTask);

        return (repository, emailService, outbox);
    }

    private static SendActivationEmailCommandHandler CreateHandler(
        Mock<IEmailNotificationRepository> repository,
        Mock<IEmailService> emailService,
        Mock<IEventBusOutboxService> outbox) =>
        new(
            repository.Object,
            emailService.Object,
            outbox.Object,
            new Mock<ILogger<SendActivationEmailCommandHandler>>().Object);
}
