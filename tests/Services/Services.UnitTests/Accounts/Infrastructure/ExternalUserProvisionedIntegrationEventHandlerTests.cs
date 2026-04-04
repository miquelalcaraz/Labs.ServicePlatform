using Microsoft.Extensions.Logging;

using Qlt.Domain.SeedWork;
using Qlt.Infrastructure.Identity.Contracts.IntegrationEvents;
using Qlt.Services.Accounts.Domain.Models.OnboardingAggregate;
using Qlt.Services.Accounts.Domain.Models.UserMembershipAggregate;
using Qlt.Services.Accounts.Domain.Repositories;
using Qlt.Services.Accounts.Infrastructure.IntegrationEvents.EventHandling;

namespace Services.UnitTests.Accounts.Infrastructure;

public class ExternalUserProvisionedIntegrationEventHandlerTests
{
    [Fact]
    public async Task Handle_WhenOnboardingExists_CreatesMembershipAndCompletesOnboarding()
    {
        var onboarding = CreateOnboardingAtProvisioningRequested();

        var unitOfWork = new Mock<IUnitOfWork>();
        unitOfWork.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var membershipRepo = new Mock<IUserMembershipRepository>();
        membershipRepo.Setup(r => r.UnitOfWork).Returns(unitOfWork.Object);
        membershipRepo.Setup(r => r.Add(It.IsAny<UserMembership>())).Returns<UserMembership>(m => m);

        var onboardingRepo = new Mock<IOnboardingProcessRepository>();
        onboardingRepo.Setup(r => r.GetAsync(onboarding.Id)).ReturnsAsync(onboarding);

        var handler = CreateHandler(membershipRepo, onboardingRepo);
        var @event = CreateProvisioningEvent(onboarding.Id, onboarding.TenantId);

        await handler.Handle(@event);

        membershipRepo.Verify(r => r.Add(It.Is<UserMembership>(m =>
            m.UserId == @event.UserId &&
            m.TenantId == @event.TenantId &&
            m.CompanyId == @event.CompanyId &&
            m.MembershipType == MembershipType.External &&
            m.RoleCode == MembershipRole.TenantAdmin &&
            m.IsPrimary == true)),
            Times.Once);
    }

    [Fact]
    public async Task Handle_WhenOnboardingExists_SetsAdminUserAndCompletesOnboarding()
    {
        var onboarding = CreateOnboardingAtProvisioningRequested();

        var unitOfWork = new Mock<IUnitOfWork>();
        unitOfWork.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var membershipRepo = new Mock<IUserMembershipRepository>();
        membershipRepo.Setup(r => r.UnitOfWork).Returns(unitOfWork.Object);
        membershipRepo.Setup(r => r.Add(It.IsAny<UserMembership>())).Returns<UserMembership>(m => m);

        var onboardingRepo = new Mock<IOnboardingProcessRepository>();
        onboardingRepo.Setup(r => r.GetAsync(onboarding.Id)).ReturnsAsync(onboarding);

        var handler = CreateHandler(membershipRepo, onboardingRepo);
        var @event = CreateProvisioningEvent(onboarding.Id, onboarding.TenantId);

        await handler.Handle(@event);

        onboarding.AdminUserId.Should().Be(@event.UserId);
        onboarding.Status.Should().Be(OnboardingStatus.Completed);
        onboarding.CurrentStep.Should().Be(OnboardingStep.Completed);
        onboardingRepo.Verify(r => r.Update(onboarding), Times.Once);
        unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenOnboardingNotFound_DoesNotCreateMembership()
    {
        var membershipRepo = new Mock<IUserMembershipRepository>();
        var onboardingRepo = new Mock<IOnboardingProcessRepository>();
        onboardingRepo.Setup(r => r.GetAsync(It.IsAny<Guid>())).ReturnsAsync((OnboardingProcess?)null);

        var handler = CreateHandler(membershipRepo, onboardingRepo);
        var @event = CreateProvisioningEvent(Guid.NewGuid(), Guid.NewGuid());

        await handler.Handle(@event);

        membershipRepo.Verify(r => r.Add(It.IsAny<UserMembership>()), Times.Never);
    }

    private static ExternalUserProvisionedIntegrationEvent CreateProvisioningEvent(
        Guid onboardingProcessId, Guid tenantId) =>
        new(
            UserId: Guid.NewGuid(),
            TenantId: tenantId,
            CompanyId: Guid.NewGuid(),
            SubscriptionId: Guid.NewGuid(),
            OnboardingProcessId: onboardingProcessId,
            Email: "admin@acme.com",
            FirstName: "John",
            LastName: "Doe");

    private static ExternalUserProvisionedIntegrationEventHandler CreateHandler(
        Mock<IUserMembershipRepository> membershipRepo,
        Mock<IOnboardingProcessRepository> onboardingRepo) =>
        new(
            new Mock<ILogger<ExternalUserProvisionedIntegrationEventHandler>>().Object,
            membershipRepo.Object,
            onboardingRepo.Object);

    private static OnboardingProcess CreateOnboardingAtProvisioningRequested()
    {
        var onboarding = OnboardingProcess.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        onboarding.Start();
        onboarding.MarkSubscriptionCreated();
        onboarding.MarkTenantCreated();
        onboarding.MarkOwnerCompanyCreated(Guid.NewGuid());
        onboarding.MarkAdminProvisioningRequested();
        return onboarding;
    }
}
