using Microsoft.Extensions.Logging;

using Qlt.Services.Accounts.Application.Commands;
using Qlt.Services.Accounts.Domain.Events;
using Qlt.Services.Accounts.Domain.Models.OnboardingAggregate;
using Qlt.Services.Accounts.Domain.Repositories;

namespace Services.UnitTests.Accounts.Application.Commands;

public class RetryOnboardingProcessCommandHandlerTests
{
    [Fact]
    public async Task Handle_WhenOnboardingNotFound_ReturnsNotFoundFailure()
    {
        var (repo, _) = CreateMocks();
        repo.Setup(r => r.GetAsync(It.IsAny<Guid>())).ReturnsAsync((OnboardingProcess?)null);
        var handler = CreateHandler(repo);

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
        var (repo, _) = CreateMocks();
        repo.Setup(r => r.GetAsync(onboarding.Id)).ReturnsAsync(onboarding);
        var handler = CreateHandler(repo);

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
        var (repo, _) = CreateMocks(saveChangesReturn: 1);
        repo.Setup(r => r.GetAsync(onboarding.Id)).ReturnsAsync(onboarding);
        var handler = CreateHandler(repo);

        var result = await handler.Handle(
            new RetryOnboardingProcessCommand(onboarding.Id, "Test retry", Guid.NewGuid()),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeTrue();
        repo.Verify(r => r.Update(onboarding), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenFailedAtAdminProvisioning_RaisesDomainEvent()
    {
        var onboarding = CreateFailedOnboarding(OnboardingStep.AdminProvisioningRequested);
        var (repo, _) = CreateMocks(saveChangesReturn: 1);
        repo.Setup(r => r.GetAsync(onboarding.Id)).ReturnsAsync(onboarding);
        var handler = CreateHandler(repo);

        await handler.Handle(
            new RetryOnboardingProcessCommand(onboarding.Id, requestId: Guid.NewGuid()),
            CancellationToken.None);

        onboarding.DomainEvents.Should().NotBeNull();
        onboarding.DomainEvents!.OfType<AdminProvisioningRequestedDomainEvent>().Should().HaveCountGreaterThanOrEqualTo(2);
    }

    [Fact]
    public async Task Handle_WhenFailedAtEarlyStep_DoesNotRaiseDomainEvent()
    {
        var onboarding = CreateFailedOnboarding(OnboardingStep.TenantCreated);
        var (repo, _) = CreateMocks(saveChangesReturn: 1);
        repo.Setup(r => r.GetAsync(onboarding.Id)).ReturnsAsync(onboarding);
        var handler = CreateHandler(repo);

        await handler.Handle(
            new RetryOnboardingProcessCommand(onboarding.Id, requestId: Guid.NewGuid()),
            CancellationToken.None);

        var domainEvents = onboarding.DomainEvents;
        if (domainEvents is not null)
            domainEvents.Should().NotContain(e => e is AdminProvisioningRequestedDomainEvent);
    }

    [Fact]
    public async Task Handle_WhenSaveChangesFails_ReturnsFailure()
    {
        var onboarding = CreateFailedOnboarding(OnboardingStep.AdminProvisioningRequested);
        var (repo, _) = CreateMocks(saveChangesReturn: 0);
        repo.Setup(r => r.GetAsync(onboarding.Id)).ReturnsAsync(onboarding);
        var handler = CreateHandler(repo);

        var result = await handler.Handle(
            new RetryOnboardingProcessCommand(onboarding.Id, requestId: Guid.NewGuid()),
            CancellationToken.None);

        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_IncrementsRetryCount()
    {
        var onboarding = CreateFailedOnboarding(OnboardingStep.AdminProvisioningRequested);
        var initialRetryCount = onboarding.RetryCount;
        var (repo, _) = CreateMocks(saveChangesReturn: 1);
        repo.Setup(r => r.GetAsync(onboarding.Id)).ReturnsAsync(onboarding);
        var handler = CreateHandler(repo);

        await handler.Handle(
            new RetryOnboardingProcessCommand(onboarding.Id, requestId: Guid.NewGuid()),
            CancellationToken.None);

        onboarding.RetryCount.Should().Be(initialRetryCount + 1);
        onboarding.Status.Should().Be(OnboardingStatus.WaitingForExternalProvisioning);
    }

    [Fact]
    public async Task Handle_WhenFailedAtAdminProvisioning_RestoresWaitingForExternalProvisioningStatus()
    {
        var onboarding = CreateFailedOnboarding(OnboardingStep.AdminProvisioningRequested);
        var (repo, _) = CreateMocks(saveChangesReturn: 1);
        repo.Setup(r => r.GetAsync(onboarding.Id)).ReturnsAsync(onboarding);
        var handler = CreateHandler(repo);

        await handler.Handle(
            new RetryOnboardingProcessCommand(onboarding.Id, requestId: Guid.NewGuid()),
            CancellationToken.None);

        onboarding.Status.Should().Be(OnboardingStatus.WaitingForExternalProvisioning);
        onboarding.CurrentStep.Should().Be(OnboardingStep.AdminProvisioningRequested);
    }

    [Fact]
    public async Task Handle_WhenFailedAtEarlyStep_StatusRemainsInProgress()
    {
        var onboarding = CreateFailedOnboarding(OnboardingStep.TenantCreated);
        var (repo, _) = CreateMocks(saveChangesReturn: 1);
        repo.Setup(r => r.GetAsync(onboarding.Id)).ReturnsAsync(onboarding);
        var handler = CreateHandler(repo);

        await handler.Handle(
            new RetryOnboardingProcessCommand(onboarding.Id, requestId: Guid.NewGuid()),
            CancellationToken.None);

        onboarding.Status.Should().Be(OnboardingStatus.InProgress);
        onboarding.CurrentStep.Should().Be(OnboardingStep.TenantCreated);
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

    private static (Mock<IOnboardingProcessRepository> Repo, Mock<object> _)
        CreateMocks(int saveChangesReturn = 1)
    {
        var unitOfWork = new Mock<Qlt.Domain.SeedWork.IUnitOfWork>();
        unitOfWork.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(saveChangesReturn);

        var repo = new Mock<IOnboardingProcessRepository>();
        repo.Setup(r => r.UnitOfWork).Returns(unitOfWork.Object);

        return (repo, new Mock<object>());
    }

    private static RetryOnboardingProcessCommandHandler CreateHandler(
        Mock<IOnboardingProcessRepository> repo) =>
        new(repo.Object, new Mock<ILogger<RetryOnboardingProcessCommandHandler>>().Object);
}
