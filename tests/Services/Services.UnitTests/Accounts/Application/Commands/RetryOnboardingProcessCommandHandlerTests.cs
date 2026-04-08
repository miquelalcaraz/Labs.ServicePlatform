using Microsoft.Extensions.Logging;

using Qlt.Platform.Messaging.Services;
using Qlt.Services.Accounts.Application.Commands;
using Qlt.Services.Accounts.Contracts.IntegrationEvents;
using Qlt.Services.Accounts.Domain.Models.OnboardingAggregate;
using Qlt.Services.Accounts.Domain.Repositories;

namespace Services.UnitTests.Accounts.Application.Commands;

public class RetryOnboardingProcessCommandHandlerTests
{
    [Fact]
    public async Task Handle_WhenOnboardingNotFound_ReturnsNotFoundFailure()
    {
        var (repo, outbox) = CreateMocks();
        repo.Setup(r => r.GetAsync(It.IsAny<Guid>())).ReturnsAsync((OnboardingProcess?)null);
        var handler = CreateHandler(repo, outbox);

        var result = await handler.Handle(
            new RetryOnboardingProcessCommand(Guid.NewGuid(), requestId: Guid.NewGuid()),
            CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Message.Should().Contain("not found");
    }

    [Fact]
    public async Task Handle_WhenOnboardingNotFailed_ReturnsConflictFailure()
    {
        var onboarding = CreateOnboardingAtStep(OnboardingStatus.InProgress, OnboardingStep.TenantCreated);
        var (repo, outbox) = CreateMocks();
        repo.Setup(r => r.GetAsync(onboarding.Id)).ReturnsAsync(onboarding);
        var handler = CreateHandler(repo, outbox);

        var result = await handler.Handle(
            new RetryOnboardingProcessCommand(onboarding.Id, requestId: Guid.NewGuid()),
            CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Message.Should().Contain("not allowed");
    }

    [Fact]
    public async Task Handle_WhenOnboardingFailed_RetriesSuccessfully()
    {
        var onboarding = CreateFailedOnboarding(OnboardingStep.AdminProvisioningRequested);
        var (repo, outbox) = CreateMocks(saveChangesReturn: 1);
        repo.Setup(r => r.GetAsync(onboarding.Id)).ReturnsAsync(onboarding);
        var handler = CreateHandler(repo, outbox);

        var result = await handler.Handle(
            new RetryOnboardingProcessCommand(onboarding.Id, "Test retry", Guid.NewGuid()),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeTrue();
        repo.Verify(r => r.Update(onboarding), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenFailedAtAdminProvisioning_RepublishesProvisioningEvent()
    {
        var onboarding = CreateFailedOnboarding(OnboardingStep.AdminProvisioningRequested);
        var (repo, outbox) = CreateMocks(saveChangesReturn: 1);
        repo.Setup(r => r.GetAsync(onboarding.Id)).ReturnsAsync(onboarding);
        var handler = CreateHandler(repo, outbox);

        await handler.Handle(
            new RetryOnboardingProcessCommand(onboarding.Id, requestId: Guid.NewGuid()),
            CancellationToken.None);

        outbox.Verify(o => o.AddAndSaveEventAsync(
            It.IsAny<CustomerAdminProvisioningRequestedIntegrationEvent>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_WhenFailedAtEarlyStep_DoesNotRepublishEvent()
    {
        var onboarding = CreateFailedOnboarding(OnboardingStep.SubscriptionCreated);
        var (repo, outbox) = CreateMocks(saveChangesReturn: 1);
        repo.Setup(r => r.GetAsync(onboarding.Id)).ReturnsAsync(onboarding);
        var handler = CreateHandler(repo, outbox);

        await handler.Handle(
            new RetryOnboardingProcessCommand(onboarding.Id, requestId: Guid.NewGuid()),
            CancellationToken.None);

        outbox.Verify(o => o.AddAndSaveEventAsync(
            It.IsAny<CustomerAdminProvisioningRequestedIntegrationEvent>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_WhenSaveChangesFails_ReturnsFailure()
    {
        var onboarding = CreateFailedOnboarding(OnboardingStep.AdminProvisioningRequested);
        var (repo, outbox) = CreateMocks(saveChangesReturn: 0);
        repo.Setup(r => r.GetAsync(onboarding.Id)).ReturnsAsync(onboarding);
        var handler = CreateHandler(repo, outbox);

        var result = await handler.Handle(
            new RetryOnboardingProcessCommand(onboarding.Id, requestId: Guid.NewGuid()),
            CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        outbox.Verify(o => o.AddAndSaveEventAsync(
            It.IsAny<CustomerAdminProvisioningRequestedIntegrationEvent>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_IncrementsRetryCount()
    {
        var onboarding = CreateFailedOnboarding(OnboardingStep.AdminProvisioningRequested);
        var initialRetryCount = onboarding.RetryCount;
        var (repo, outbox) = CreateMocks(saveChangesReturn: 1);
        repo.Setup(r => r.GetAsync(onboarding.Id)).ReturnsAsync(onboarding);
        var handler = CreateHandler(repo, outbox);

        await handler.Handle(
            new RetryOnboardingProcessCommand(onboarding.Id, requestId: Guid.NewGuid()),
            CancellationToken.None);

        onboarding.RetryCount.Should().Be(initialRetryCount + 1);
        onboarding.Status.Should().Be(OnboardingStatus.InProgress);
    }

    private static OnboardingProcess CreateOnboardingAtStep(OnboardingStatus status, OnboardingStep step)
    {
        var onboarding = OnboardingProcess.Create(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            companyLegalName: "Test Corp",
            subscriptionPlanCode: "STANDARD",
            adminEmail: "admin@test.com",
            adminFirstName: "John",
            adminLastName: "Doe",
            tenantName: "Test Tenant");
        onboarding.Start();
        onboarding.MarkSubscriptionCreated();

        if (step >= OnboardingStep.TenantCreated)
            onboarding.MarkTenantCreated();

        return onboarding;
    }

    private static OnboardingProcess CreateFailedOnboarding(OnboardingStep failAtStep)
    {
        var onboarding = OnboardingProcess.Create(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            companyLegalName: "Test Corp",
            subscriptionPlanCode: "STANDARD",
            adminEmail: "admin@test.com",
            adminFirstName: "John",
            adminLastName: "Doe",
            tenantName: "Test Tenant");
        onboarding.Start();
        onboarding.MarkSubscriptionCreated();

        if (failAtStep >= OnboardingStep.TenantCreated)
            onboarding.MarkTenantCreated();
        if (failAtStep >= OnboardingStep.OwnerCompanyCreated)
            onboarding.MarkOwnerCompanyCreated(Guid.NewGuid());
        if (failAtStep >= OnboardingStep.AdminProvisioningRequested)
            onboarding.MarkAdminProvisioningRequested();

        onboarding.Fail("ERR001", "Test failure");
        return onboarding;
    }

    private static (Mock<IOnboardingProcessRepository> Repo, Mock<IEventBusOutboxService> Outbox)
        CreateMocks(int saveChangesReturn = 1)
    {
        var unitOfWork = new Mock<Qlt.Domain.SeedWork.IUnitOfWork>();
        unitOfWork.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(saveChangesReturn);

        var repo = new Mock<IOnboardingProcessRepository>();
        repo.Setup(r => r.UnitOfWork).Returns(unitOfWork.Object);

        var outbox = new Mock<IEventBusOutboxService>();

        return (repo, outbox);
    }

    private static RetryOnboardingProcessCommandHandler CreateHandler(
        Mock<IOnboardingProcessRepository> repo,
        Mock<IEventBusOutboxService> outbox) =>
        new(repo.Object, outbox.Object, new Mock<ILogger<RetryOnboardingProcessCommandHandler>>().Object);
}
