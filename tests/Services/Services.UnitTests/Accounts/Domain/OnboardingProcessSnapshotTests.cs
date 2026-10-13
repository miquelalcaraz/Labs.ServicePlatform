using Miqa.Services.Accounts.Domain.Models.OnboardingAggregate;

namespace Services.UnitTests.Accounts.Domain;

public class OnboardingProcessSnapshotTests
{
    [Fact]
    public void Create_WithSnapshotData_StoresSnapshotProperties()
    {
        var onboarding = OnboardingProcess.Create(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            companyLegalName: "Acme Corp",
            subscriptionPlanCode: "B2B_PRO",
            adminEmail: "admin@acme.com",
            adminFirstName: "Laura",
            adminLastName: "Martinez",
            tenantName: "Acme Tenant");

        onboarding.CompanyLegalName.Should().Be("Acme Corp");
        onboarding.SubscriptionPlanCode.Should().Be("B2B_PRO");
        onboarding.AdminEmail.Should().Be("admin@acme.com");
        onboarding.AdminFirstName.Should().Be("Laura");
        onboarding.AdminLastName.Should().Be("Martinez");
        onboarding.TenantName.Should().Be("Acme Tenant");
    }

    [Fact]
    public void Create_WithoutSnapshotData_LeavesSnapshotPropertiesNull()
    {
        var onboarding = OnboardingProcess.Create(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());

        onboarding.CompanyLegalName.Should().BeNull();
        onboarding.SubscriptionPlanCode.Should().BeNull();
        onboarding.AdminEmail.Should().BeNull();
        onboarding.AdminFirstName.Should().BeNull();
        onboarding.AdminLastName.Should().BeNull();
        onboarding.TenantName.Should().BeNull();
    }

    [Fact]
    public void Create_WithCorrelationId_StoresCorrelationId()
    {
        var correlationId = Guid.NewGuid();

        var onboarding = OnboardingProcess.Create(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            correlationId: correlationId);

        onboarding.CorrelationId.Should().Be(correlationId);
    }

    [Fact]
    public void Create_WithoutCorrelationId_LeavesCorrelationIdNull()
    {
        var onboarding = OnboardingProcess.Create(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());

        onboarding.CorrelationId.Should().BeNull();
    }

    [Fact]
    public void MarkAdminMembershipCreated_WithMembershipId_StoresId()
    {
        var onboarding = CreateOnboardingAtAdminUserProvisioned();
        var membershipId = Guid.NewGuid();

        onboarding.MarkAdminMembershipCreated(membershipId);

        onboarding.AdminMembershipId.Should().Be(membershipId);
        onboarding.CurrentStep.Should().Be(OnboardingStep.AdminMembershipCreated);
    }

    [Fact]
    public void MarkAdminMembershipCreated_WithoutMembershipId_LeavesNull()
    {
        var onboarding = CreateOnboardingAtAdminUserProvisioned();

        onboarding.MarkAdminMembershipCreated();

        onboarding.AdminMembershipId.Should().BeNull();
        onboarding.CurrentStep.Should().Be(OnboardingStep.AdminMembershipCreated);
    }

    private static OnboardingProcess CreateOnboardingAtAdminUserProvisioned()
    {
        var onboarding = OnboardingProcess.Create(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        onboarding.Start();
        onboarding.MarkSubscriptionCreated();
        onboarding.MarkTenantCreated();
        onboarding.MarkOwnerCompanyCreated(Guid.NewGuid());
        onboarding.MarkAdminProvisioningRequested();
        onboarding.MarkAdminUserProvisioned(Guid.NewGuid());
        return onboarding;
    }
}
