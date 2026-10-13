using Microsoft.Extensions.Logging;

using Miqa.Domain.SeedWork;
using Miqa.Services.Notifications.Application.Commands;
using Miqa.Services.Notifications.Domain.Models;
using Miqa.Services.Notifications.Domain.Repositories;
using Miqa.Services.Notifications.Domain.Services;

namespace Services.UnitTests.Notifications.Application.Commands;

public class SendCompanyWelcomeEmailsCommandHandlerTests
{
    private static readonly Guid CompanyId = Guid.NewGuid();
    private const string TradeName = "Acme";
    private const string LegalName = "Acme Corp S.L.";
    private const string CountryCode = "ES";

    [Fact]
    public async Task Handle_PersistsTwoEmailNotificationsAndSendsEmails()
    {
        var (repository, emailService) = CreateMocks();
        var handler = CreateHandler(repository, emailService);

        var result = await handler.Handle(CreateCommand(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();

        repository.Verify(r => r.Add(It.Is<EmailNotification>(n =>
            n.Type == EmailType.WelcomeOrganization &&
            n.Status == NotificationStatus.Sent)),
            Times.Once);

        repository.Verify(r => r.Add(It.Is<EmailNotification>(n =>
            n.Type == EmailType.SalesNotification &&
            n.Status == NotificationStatus.Sent)),
            Times.Once);

        emailService.Verify(s => s.SendAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()),
            Times.Exactly(2));

        repository.Verify(r => r.UnitOfWork, Times.AtLeastOnce);
    }

    [Fact]
    public async Task Handle_WelcomeEmailSentToGeneratedAddress()
    {
        var (repository, emailService) = CreateMocks();
        var handler = CreateHandler(repository, emailService);

        await handler.Handle(CreateCommand(), CancellationToken.None);

        repository.Verify(r => r.Add(It.Is<EmailNotification>(n =>
            n.Type == EmailType.WelcomeOrganization &&
            n.To == "admin@acme.com")),
            Times.Once);
    }

    [Fact]
    public async Task Handle_SalesNotificationSentToSalesTeam()
    {
        var (repository, emailService) = CreateMocks();
        var handler = CreateHandler(repository, emailService);

        await handler.Handle(CreateCommand(), CancellationToken.None);

        repository.Verify(r => r.Add(It.Is<EmailNotification>(n =>
            n.Type == EmailType.SalesNotification &&
            n.To == "sales@example.com")),
            Times.Once);
    }

    [Fact]
    public async Task Handle_WhenFirstEmailFails_ExceptionPropagates()
    {
        var (repository, emailService) = CreateMocks();
        emailService
            .Setup(s => s.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ThrowsAsync(new InvalidOperationException("SMTP failure"));

        var handler = CreateHandler(repository, emailService);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => handler.Handle(CreateCommand(), CancellationToken.None));
    }

    [Fact]
    public async Task Handle_WelcomeEmailSubjectIncludesTradeName()
    {
        var (repository, emailService) = CreateMocks();
        var handler = CreateHandler(repository, emailService);

        await handler.Handle(CreateCommand(), CancellationToken.None);

        repository.Verify(r => r.Add(It.Is<EmailNotification>(n =>
            n.Type == EmailType.WelcomeOrganization &&
            n.Subject == $"Welcome to the platform, {TradeName}!")),
            Times.Once);
    }

    [Fact]
    public async Task Handle_SalesNotificationBodyContainsCompanyDetails()
    {
        var (repository, emailService) = CreateMocks();
        var handler = CreateHandler(repository, emailService);

        await handler.Handle(CreateCommand(), CancellationToken.None);

        repository.Verify(r => r.Add(It.Is<EmailNotification>(n =>
            n.Type == EmailType.SalesNotification &&
            n.Body.Contains(TradeName) &&
            n.Body.Contains(LegalName) &&
            n.Body.Contains(CountryCode))),
            Times.Once);
    }

    private static SendCompanyWelcomeEmailsCommand CreateCommand() =>
        new(CompanyId, TradeName, LegalName, CountryCode);

    private static (Mock<IEmailNotificationRepository> Repository, Mock<IEmailService> EmailService) CreateMocks()
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

        return (repository, emailService);
    }

    private static SendCompanyWelcomeEmailsCommandHandler CreateHandler(
        Mock<IEmailNotificationRepository> repository,
        Mock<IEmailService> emailService) =>
        new(
            repository.Object,
            emailService.Object,
            new Mock<ILogger<SendCompanyWelcomeEmailsCommandHandler>>().Object);
}
