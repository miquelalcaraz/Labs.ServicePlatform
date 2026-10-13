using Microsoft.Extensions.Logging;

using Miqa.Domain.SeedWork;
using Miqa.Services.Accounts.Application.Commands;
using Miqa.Services.Accounts.Domain.Models.OnboardingAggregate;
using Miqa.Services.Accounts.Domain.Models.UserMembershipAggregate;
using Miqa.Services.Accounts.Domain.Repositories;

namespace Services.UnitTests.Accounts.Application.Commands;

public class CompleteOnboardingCommandHandlerTests
{
    [Fact]
    public async Task Handle_WhenOnboardingExists_CreatesMembershipAndCompletesOnboarding()
    {
        var onboarding = CreateOnboardingAtProvisioningRequested();
        var (onboardingRepo, membershipRepo, unitOfWork) = CreateMocks(onboarding);
        var handler = CreateHandler(onboardingRepo, membershipRepo);

        var command = CreateCommand(onboarding.Id, onboarding.TenantId);
        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        onboarding.AdminUserId.Should().Be(command.UserId);
        onboarding.Status.Should().Be(OnboardingStatus.Completed);
        onboarding.CurrentStep.Should().Be(OnboardingStep.Completed);
        membershipRepo.Verify(r => r.Add(It.Is<UserMembership>(m =>
            m.UserId == command.UserId &&
            m.TenantId == command.TenantId &&
            m.CompanyId == command.CompanyId &&
            m.MembershipType == MembershipType.External &&
            m.RoleCode == MembershipRole.TenantAdmin &&
            m.IsPrimary == true)),
            Times.Once);
        onboardingRepo.Verify(r => r.Update(onboarding), Times.Once);
        unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenOnboardingExists_ReturnsResultWithMembershipAndOnboardingData()
    {
        var onboarding = CreateOnboardingAtProvisioningRequested(companyLegalName: "Acme Corp", subscriptionPlanCode: "starter");
        var (onboardingRepo, membershipRepo, _) = CreateMocks(onboarding);
        var handler = CreateHandler(onboardingRepo, membershipRepo);

        var result = await handler.Handle(CreateCommand(onboarding.Id, onboarding.TenantId), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.AdminMembershipId.Should().NotBeEmpty();
        result.Value.CompanyLegalName.Should().Be("Acme Corp");
        result.Value.SubscriptionPlanCode.Should().Be("starter");
    }

    [Fact]
    public async Task Handle_WhenOnboardingNotFound_ReturnsNotFound()
    {
        var unitOfWork = new Mock<IUnitOfWork>();
        var onboardingRepo = new Mock<IOnboardingProcessRepository>();
        onboardingRepo.Setup(r => r.GetAsync(It.IsAny<Guid>())).ReturnsAsync((OnboardingProcess?)null);
        var membershipRepo = new Mock<IUserMembershipRepository>();

        var handler = CreateHandler(onboardingRepo, membershipRepo);

        var result = await handler.Handle(CreateCommand(Guid.NewGuid(), Guid.NewGuid()), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        membershipRepo.Verify(r => r.Add(It.IsAny<UserMembership>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenOnboardingAlreadyCompleted_ReturnsSuccessWithExistingData()
    {
        var onboarding = CreateCompletedOnboarding();
        var (onboardingRepo, membershipRepo, unitOfWork) = CreateMocks(onboarding);
        var handler = CreateHandler(onboardingRepo, membershipRepo);

        var result = await handler.Handle(CreateCommand(onboarding.Id, onboarding.TenantId), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        membershipRepo.Verify(r => r.Add(It.IsAny<UserMembership>()), Times.Never);
        unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    private static CompleteOnboardingCommand CreateCommand(Guid onboardingProcessId, Guid tenantId) =>
        new(
            OnboardingProcessId: onboardingProcessId,
            UserId: Guid.NewGuid(),
            TenantId: tenantId,
            CompanyId: Guid.NewGuid(),
            SubscriptionId: Guid.NewGuid(),
            Email: "admin@acme.com");

    private static OnboardingProcess CreateOnboardingAtProvisioningRequested(
        string? companyLegalName = null,
        string? subscriptionPlanCode = null)
    {
        var onboarding = OnboardingProcess.Create(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            companyLegalName: companyLegalName,
            subscriptionPlanCode: subscriptionPlanCode);
        onboarding.Start();
        onboarding.MarkSubscriptionCreated();
        onboarding.MarkTenantCreated();
        onboarding.MarkOwnerCompanyCreated(Guid.NewGuid());
        onboarding.MarkAdminProvisioningRequested();
        return onboarding;
    }

    private static OnboardingProcess CreateCompletedOnboarding()
    {
        var onboarding = OnboardingProcess.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        onboarding.Start();
        onboarding.MarkSubscriptionCreated();
        onboarding.MarkTenantCreated();
        onboarding.MarkOwnerCompanyCreated(Guid.NewGuid());
        onboarding.MarkAdminProvisioningRequested();
        onboarding.MarkAdminUserProvisioned(Guid.NewGuid());
        onboarding.MarkAdminMembershipCreated(Guid.NewGuid());
        onboarding.Complete();
        return onboarding;
    }

    private static (Mock<IOnboardingProcessRepository> OnboardingRepo, Mock<IUserMembershipRepository> MembershipRepo, Mock<IUnitOfWork> UnitOfWork) CreateMocks(
        OnboardingProcess onboarding)
    {
        var unitOfWork = new Mock<IUnitOfWork>();
        unitOfWork.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var onboardingRepo = new Mock<IOnboardingProcessRepository>();
        onboardingRepo.Setup(r => r.GetAsync(onboarding.Id)).ReturnsAsync(onboarding);

        var membershipRepo = new Mock<IUserMembershipRepository>();
        membershipRepo.Setup(r => r.UnitOfWork).Returns(unitOfWork.Object);
        membershipRepo.Setup(r => r.Add(It.IsAny<UserMembership>())).Returns<UserMembership>(m => m);

        return (onboardingRepo, membershipRepo, unitOfWork);
    }

    private static CompleteOnboardingCommandHandler CreateHandler(
        Mock<IOnboardingProcessRepository> onboardingRepo,
        Mock<IUserMembershipRepository> membershipRepo) =>
        new(onboardingRepo.Object, membershipRepo.Object,
            new Mock<ILogger<CompleteOnboardingCommandHandler>>().Object);
}
